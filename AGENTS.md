# AGENTS.md — LTC 项目协作指南

本文件面向在本仓库中工作的 AI 代理 / 自动化工具。

## 项目概况

- Godot 4.7 .NET（mono）项目，引擎可执行文件：
  `C:\Program Files\Godot\Godot_v4.7-stable_mono_win64.exe`
- 唯一功能：编辑器插件 **LTC**（`addons/LTC/`），为 `RichTextLabel` 提供
  `[ltc="注释"]正文[/ltc]` 富文本注释标签（注释以小字号居中显示在正文上方）。
- 主场景 `main.tscn` 内有三个挂接了 `LtcRichTextLabel.cs` 的 `RichTextLabel`
  作为效果验收用例（含 `visible_ratio` 打字机场景）。

## 构建 / 运行 / 验证

```powershell
dotnet build LTC.csproj            # 输出由 Godot.NET.Sdk 重定向到 .godot/mono/temp/bin/Debug
```

- dotnet 构建需要写用户目录（`~/.dotnet` 哨兵、NuGet 缓存）；沙箱拦截时请提权重试，
  不要设置 `DOTNET_CLI_HOME`（会离线找不到 Godot.NET.Sdk）。
- 运行游戏进程（截图验证等）需要写 `%APPDATA%\Godot`，被沙箱拦截会直接导致引擎
  启动即崩溃（signal 11）——请提权运行，不要当作代码 bug 排查。
- 截图验证套路：临时建一个实例化 `main.tscn` 的场景 + GDScript 等待数帧后
  `get_viewport().get_texture().get_image().save_png()` 再 `quit()`；
  验证完删除临时文件。

## 架构（两段式）

`RichTextEffect` 无法直接绘制新文字，因此：

1. `LtcAnnotationEffect.cs`（`RichTextEffect`）：`_ProcessCustomFX` 回调中
   只**记录**被标注字形的 `Transform.Origin`、步进、颜色、字符下标；
2. `LtcRichTextLabel.cs`（挂在 Label 上，`[GlobalClass, Tool]`）：
   安装效果、幂等地腾出顶部空间（StyleBox 边距 + 补偿 `custom_maximum_size`）、
   在 `_Draw()` 中消费坐标并绘制注释（脚本 `_Draw` 在原生文本绘制之后执行）。

## 已踩过的引擎坑（改动前必读）

- **C# 的 `bbcode` 字段必须加 `[Export]`**：普通字段不进脚本属性列表，
  `RichTextEffect::get_bbcode()` 读不到，标签不会被识别（GDScript 无此问题）。
- **自定义效果标签不支持 `[ltc="X"]` 简写**：解析器会把参数连带进识别名
  （`rich_text_label.cpp` 约 6200 行）。插件用正则把 `[ltc="X"]` 幂等改写为
  `[ltc ltc="X"]` 再解析；`_Set` 钩子拦截运行时改文本并延迟重建。
- **`ParseBbcode()` 不回写 `text` 属性**：段区间必须基于本地算出的改写结果，
  重新读 `Text` 拿到的永远是原文。
- **字形真实位置在 `charFX.Transform.Origin`**；`Offset` 是整形缓冲内偏移（常为 0）。
  CJK 字形会伴随一次 `GlyphIndex == 0` 的附加回调，必须过滤。
- **`set_custom_effects` 不触发重新解析**；且被 `visible_ratio` 截断的字形
  **不会回调** `_ProcessCustomFX`（整段显隐/逐字跟随要靠 Label 侧按
  `VisibleCharacters` 与段区间判断，见 `_Draw`）。
- **`custom_maximum_size` 会钳制 `get_combined_minimum_size`**：顶部边距推高
  内容后必须同步补偿，否则正文底部被裁剪。
- **场景加载时 `visible_ratio` 按含标签原文总长折算**，重新解析后要手动重算
  （`set_visible_ratio` 数值不变时直接返回，需绕过 setter 用
  `GetTotalCharacterCount()` 折算）。
- **[Tool] 脚本在编辑器的运行时修改会被序列化进 tscn**：保存场景会写入自动添加的
  效果/样式覆盖。所有这类逻辑必须幂等 + 去重，防止重载累积。

## 代码风格

- C# 两空格缩进、K&R 花括号、成员/方法带中文 XML doc 或行注释；
- 私有字段 `_camelCase`；导出属性 PascalCase；
- 代码中的关键 workaround 必须保留解释性注释（标注引擎行为与出处）。
