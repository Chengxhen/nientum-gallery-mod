# NientumGallery 游戏内鉴赏 Mod

English: [README.en.md](README.en.md)

《Nientum - Op.ZERO》游戏内 CG / 立绘 / 音乐鉴赏室，BepInEx 5 + Harmony 实现，入口挂在标题界面，所有内容直接读取游戏本体资源（Addressables CG、FMOD 音乐、Spine 立绘）。

本仓库只包含 Mod 源码。不包含游戏本体、CG、立绘、音乐、字体或任何提取资源。

## 功能

- 6 个页签：谢幕 CG、原画、剧情/尾声、立绘、差分、曲目
- CG 卡片、查看器缩放拖动、悬停/选中金边、章节筛选
- 剧场式播放器：曲目卡、试听/整曲、循环、播放器脚灯与光环频谱、底栏自动隐藏
- 打开鉴赏室时压低主界面 BGM，退出恢复
- FMOD 事件缺失的曲目用 Media Foundation 解码 FLAC 回退播放

## 目录

- `Plugin.cs` — 入口、UI 构建、CG/音乐逻辑、Harmony 补丁
- `GalleryData.cs` — 游戏资源索引（Addressables / FMOD bank）
- `PlayerCardStage.cs`、`PlayerVizOverlay.cs`、`SpectrumGraphic.cs`、`UiSprites.cs` — 播放器与可视化
- `FlacAudio.cs` + `NientumFlac.cpp` — FLAC 回退解码（Media Foundation）
- `NientumGallery.csproj`

## 构建

需要游戏目录中的 `BepInEx\core` 与 `Nientum_Data\Managed` 引用，用 `GameDir` 指定安装目录：

```bat
dotnet build NientumGallery.csproj -c Release ^
  -p:GameDir="D:\Games\Liora"
```

FLAC 原生解码器用 MSVC 编译（`#pragma comment` 已链接所需库）：

```bat
cl /LD /O2 /EHsc NientumFlac.cpp /Fe:NientumFlac.dll
```

把 `NientumFlac.dll` 和字体/缩略图等资源放到 `BepInEx\plugins\NientumGalleryAssets\`，`NientumGallery.dll` 放到 `BepInEx\plugins\`。

## 使用

进入游戏标题界面后点击“鉴赏室”入口。`BepInEx\config\` 下会生成配置项（`AutoOpen`、`StartTab`、`StartPage`、`StartViewer` 等调试开关）。

## 已知限制

- 曲目页有一小部分曲子在当前 FMOD bank 中找不到对应事件（日志 `music bankIndex miss`），会显示“该曲目在游戏音频库中不存在”，这是游戏 bank 本身没有加载，不是 Mod 能靠前缀匹配修好的。
- 依赖游戏当前版本的程序集；游戏更新后可能需要重新对照 `Assembly-CSharp`。
- 只支持 Windows + BepInEx 5。

## 发布包

Release 提供 `NientumGallery_Mod_20261118_no-music.zip`：`NientumGallery.dll` + `NientumGalleryAssets`（CG、缩略图、字体、UI、FLAC 解码器）+ 内置 BepInEx 5.4 框架，解压覆盖游戏根目录即可。已按要求移除 `NientumGalleryAssets\music`，FMOD 缺失曲目的 FLAC 回退会不可用。源码构建仍需本机安装 BepInEx 5.4。

## 版权与下架声明

本项目为非官方玩家工具，与《Nientum - Op.ZERO》的开发商、发行商无隶属关系。游戏及其 CG、立绘、音乐、字体、剧本等素材版权归 QueseraGames 所有；发布包中的素材仅限个人学习交流，禁止商用、二次售卖或冒充官方。如权利人认为侵权，请通过本仓库 Issue 联系，核实后立即下架相关源码与发布物。

## 许可

代码以 MIT 许可发布，见 [LICENSE](LICENSE)。游戏素材与 BepInEx/FMOD 等第三方组件不在许可范围内。