#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#include <mfapi.h>
#include <mfidl.h>
#include <mfreadwrite.h>
#include <wrl/client.h>
#include <vector>
#include <cstring>
#include <cstdlib>

#pragma comment(lib, "mfplat.lib")
#pragma comment(lib, "mfreadwrite.lib")
#pragma comment(lib, "mfuuid.lib")
#pragma comment(lib, "ole32.lib")

using Microsoft::WRL::ComPtr;

extern "C" __declspec(dllexport) int np_decode_flac(
    const wchar_t* path, float** out_pcm, int* out_frames, int* out_channels, int* out_sr)
{
    if (!path || !out_pcm || !out_frames || !out_channels || !out_sr) return -1;
    *out_pcm = nullptr;
    *out_frames = 0;
    *out_channels = 2;
    *out_sr = 48000;

    HRESULT hrCo = CoInitializeEx(nullptr, COINIT_MULTITHREADED);
    MFStartup(MF_VERSION);

    ComPtr<IMFSourceReader> reader;
    HRESULT hr = MFCreateSourceReaderFromURL(path, nullptr, &reader);
    if (FAILED(hr) || !reader) {
        if (SUCCEEDED(hrCo)) CoUninitialize();
        return -2;
    }

    ComPtr<IMFMediaType> req;
    MFCreateMediaType(&req);
    req->SetGUID(MF_MT_MAJOR_TYPE, MFMediaType_Audio);
    req->SetGUID(MF_MT_SUBTYPE, MFAudioFormat_Float);
    req->SetUINT32(MF_MT_AUDIO_NUM_CHANNELS, 2);
    req->SetUINT32(MF_MT_AUDIO_SAMPLES_PER_SECOND, 48000);
    req->SetUINT32(MF_MT_AUDIO_BITS_PER_SAMPLE, 32);
    hr = reader->SetCurrentMediaType(MF_SOURCE_READER_FIRST_AUDIO_STREAM, nullptr, req.Get());
    if (FAILED(hr)) {
        if (SUCCEEDED(hrCo)) CoUninitialize();
        return -3;
    }

    ComPtr<IMFMediaType> actual;
    if (SUCCEEDED(reader->GetCurrentMediaType(MF_SOURCE_READER_FIRST_AUDIO_STREAM, &actual))) {
        UINT32 ch = 2, sr = 48000;
        actual->GetUINT32(MF_MT_AUDIO_NUM_CHANNELS, &ch);
        actual->GetUINT32(MF_MT_AUDIO_SAMPLES_PER_SECOND, &sr);
        *out_channels = static_cast<int>(ch);
        *out_sr = static_cast<int>(sr);
    }

    std::vector<float> pcm;
    pcm.reserve(48000 * 2 * 180);

    for (;;) {
        DWORD flags = 0;
        ComPtr<IMFSample> sample;
        hr = reader->ReadSample(MF_SOURCE_READER_FIRST_AUDIO_STREAM, 0, nullptr, &flags, nullptr, &sample);
        if (FAILED(hr) || (flags & MF_SOURCE_READERF_ENDOFSTREAM)) break;
        if (!sample) continue;

        ComPtr<IMFMediaBuffer> buf;
        if (FAILED(sample->ConvertToContiguousBuffer(&buf)) || !buf) continue;
        BYTE* data = nullptr;
        DWORD len = 0;
        if (SUCCEEDED(buf->Lock(&data, nullptr, &len)) && data && len >= sizeof(float)) {
            size_t nf = len / sizeof(float);
            size_t old = pcm.size();
            pcm.resize(old + nf);
            std::memcpy(pcm.data() + old, data, nf * sizeof(float));
            buf->Unlock();
        }
    }

    if (SUCCEEDED(hrCo)) CoUninitialize();
    if (pcm.empty()) return -4;

    float* mem = static_cast<float*>(std::malloc(pcm.size() * sizeof(float)));
    if (!mem) return -5;
    std::memcpy(mem, pcm.data(), pcm.size() * sizeof(float));

    *out_pcm = mem;
    *out_frames = static_cast<int>(pcm.size() / std::max(1, *out_channels));
    return 0;
}

extern "C" __declspec(dllexport) void np_free_pcm(float* pcm)
{
    if (pcm) std::free(pcm);
}
