using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;

namespace NientumGallery
{
    /// <summary>
    /// FMOD 事件缺失曲目的无损 FLAC 回退播放器：通过 NientumFlac.dll 解码为 float PCM，
    /// 再直接交由 FMOD CoreSystem（RuntimeManager.CoreSystem）创建内存音频并播放，
    /// 即使 Unity 内置 AudioSource 被禁用也能正常发声并支持音量、暂停、进度与循环。
    /// </summary>
    internal static class FlacAudio
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibraryW(string path);

        [DllImport("NientumFlac", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        private static extern int np_decode_flac(string path, out IntPtr outPcm, out int outFrames, out int outChannels, out int outSr);

        [DllImport("NientumFlac", CallingConvention = CallingConvention.Cdecl)]
        private static extern void np_free_pcm(IntPtr pcm);

        private static bool _dllLoaded;
        private static Dictionary<string, string> _titleToRel;
        private static FMOD.Sound _sound;
        private static FMOD.Channel _channel;
        private static bool _paused;
        private static int _lengthMs;
        private static int _gen;

        public static bool IsPlayingOrPaused
        {
            get
            {
                if (!_sound.hasHandle() || !_channel.hasHandle()) return false;
                if (_paused) return true;
                try { return _channel.isPlaying(out bool p) == FMOD.RESULT.OK && p; }
                catch { return false; }
            }
        }

        public static bool IsAlive
        {
            get
            {
                if (!_sound.hasHandle() || !_channel.hasHandle()) return false;
                try { return _channel.isPlaying(out bool p) == FMOD.RESULT.OK && p; }
                catch { return false; }
            }
        }

        private static bool EnsureDll(string assetRoot)
        {
            if (_dllLoaded) return true;
            try
            {
                string dll = Path.Combine(assetRoot, "NientumFlac.dll");
                if (!File.Exists(dll)) return false;
                if (LoadLibraryW(dll) != IntPtr.Zero) _dllLoaded = true;
            }
            catch { }
            return _dllLoaded;
        }

        private static void EnsureMap(string assetRoot)
        {
            if (_titleToRel != null) return;
            _titleToRel = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                string mapPath = Path.Combine(assetRoot, "music_paths.txt");
                if (!File.Exists(mapPath)) return;
                foreach (var line in File.ReadAllLines(mapPath))
                {
                    int tab = line.IndexOf('\t');
                    if (tab <= 0) continue;
                    string t = line.Substring(0, tab).Trim();
                    string rel = line.Substring(tab + 1).Trim();
                    if (!string.IsNullOrEmpty(t) && !string.IsNullOrEmpty(rel)) _titleToRel[t] = rel;
                }
            }
            catch { }
        }

        public static string ResolveFlacPath(string assetRoot, string title)
        {
            if (string.IsNullOrEmpty(title)) return null;
            EnsureMap(assetRoot);
            if (!_titleToRel.TryGetValue(title, out string rel)) return null;
            string fname = Path.GetFileName(rel.Replace('/', Path.DirectorySeparatorChar));
            string local = Path.Combine(Path.Combine(assetRoot, "music"), fname);
            if (File.Exists(local)) return local;
            return null;
        }

        public static IEnumerator LoadAndPlay(string assetRoot, GameObject host, string title, bool loop,
            Action<bool> onDone)
        {
            Stop();
            int myGen = _gen;
            if (!EnsureDll(assetRoot)) { onDone?.Invoke(false); yield break; }
            string path = ResolveFlacPath(assetRoot, title);
            if (string.IsNullOrEmpty(path)) { onDone?.Invoke(false); yield break; }

            IntPtr pcmPtr = IntPtr.Zero;
            int frames = 0, ch = 2, sr = 48000, rc = -1;
            var task = Task.Run(() =>
            {
                rc = np_decode_flac(path, out pcmPtr, out frames, out ch, out sr);
            });
            while (!task.IsCompleted) yield return null;

            if (myGen != _gen || rc != 0 || pcmPtr == IntPtr.Zero || frames <= 0 || ch <= 0 || sr <= 0)
            {
                if (pcmPtr != IntPtr.Zero) np_free_pcm(pcmPtr);
                onDone?.Invoke(false);
                yield break;
            }

            bool ok = false;
            try
            {
                var sys = FMODUnity.RuntimeManager.CoreSystem;
                var ex = new FMOD.CREATESOUNDEXINFO();
                ex.cbsize = Marshal.SizeOf(typeof(FMOD.CREATESOUNDEXINFO));
                ex.numchannels = ch;
                ex.defaultfrequency = sr;
                ex.format = FMOD.SOUND_FORMAT.PCMFLOAT;
                ex.length = (uint)(frames * ch * sizeof(float));

                FMOD.MODE mode = FMOD.MODE.OPENMEMORY | FMOD.MODE.OPENRAW | FMOD.MODE.CREATESAMPLE
                    | (loop ? FMOD.MODE.LOOP_NORMAL : FMOD.MODE.LOOP_OFF);

                FMOD.Sound snd;
                if (sys.createSound(pcmPtr, mode, ref ex, out snd) == FMOD.RESULT.OK && snd.hasHandle())
                {
                    FMOD.ChannelGroup master;
                    sys.getMasterChannelGroup(out master);
                    FMOD.Channel channel;
                    if (sys.playSound(snd, master, true, out channel) == FMOD.RESULT.OK && channel.hasHandle())
                    {
                        _sound = snd;
                        _channel = channel;
                        _paused = false;
                        _lengthMs = Mathf.RoundToInt((float)frames / sr * 1000f);
                        _channel.setVolume(0f);
                        _channel.setPaused(false);
                        ok = true;
                    }
                    else
                    {
                        try { snd.release(); } catch { }
                    }
                }
            }
            catch { ok = false; }
            finally
            {
                np_free_pcm(pcmPtr);
            }

            onDone?.Invoke(ok);
        }

        public static void SetVolume(float v)
        {
            if (_channel.hasHandle())
            {
                try { _channel.setVolume(Mathf.Clamp01(v)); } catch { }
            }
        }

        public static void SetPaused(bool p)
        {
            _paused = p;
            if (_channel.hasHandle())
            {
                try { _channel.setPaused(p); } catch { }
            }
        }

        public static int PositionMs()
        {
            if (!_channel.hasHandle()) return 0;
            try
            {
                if (_channel.getPosition(out uint ms, FMOD.TIMEUNIT.MS) == FMOD.RESULT.OK)
                    return (int)ms;
            }
            catch { }
            return 0;
        }

        public static int LengthMs()
        {
            if (_sound.hasHandle())
            {
                try
                {
                    if (_sound.getLength(out uint ms, FMOD.TIMEUNIT.MS) == FMOD.RESULT.OK && ms > 0)
                        return (int)ms;
                }
                catch { }
            }
            return _lengthMs;
        }

        public static void SeekFrac(float f)
        {
            int len = LengthMs();
            if (!_channel.hasHandle() || len <= 0) return;
            uint target = (uint)Mathf.Clamp(Mathf.RoundToInt(f * len), 0, Math.Max(0, len - 50));
            try { _channel.setPosition(target, FMOD.TIMEUNIT.MS); } catch { }
        }

        public static void Stop()
        {
            _gen++;
            _paused = false;
            _lengthMs = 0;
            if (_channel.hasHandle())
            {
                try { _channel.stop(); } catch { }
                _channel.clearHandle();
            }
            if (_sound.hasHandle())
            {
                try { _sound.release(); } catch { }
                _sound.clearHandle();
            }
        }
    }
}
