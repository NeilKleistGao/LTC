using Godot;
using System.Collections.Generic;

/// <summary>
/// [ltc="annotation"]...[/ltc] 标签的 RichTextEffect。
/// 与 ProjectM 的 TextFlipVEffect 同构：[GlobalClass] + 公开的 bbcode 字段，
/// 通过 RichTextLabel.custom_effects 挂载（LtcRichTextLabel 会自动安装）。
/// 自身不修改字形，只在绘制阶段记录每个被标注字形的坐标与可见性；
/// 注释文字由持有的 LtcRichTextLabel 在 _Draw() 中绘制。
/// </summary>
[GlobalClass]
public partial class LtcAnnotationEffect : RichTextEffect {
  // 引擎通过名为 "bbcode" 的脚本属性识别此效果对应的标签名。
  // 注意：必须加 [Export]，C# 的普通字段不会进入脚本属性列表，引擎将识别不到标签。
  [Export] public string bbcode = "ltc";

  public struct AnnotatedGlyph {
    public string Annotation; // 注释文字（标签参数）
    public Vector2 Position;  // 字形笔位（Label 本地坐标，Y 为正文基线）
    public float Advance;     // 字形步进宽度
    public Color Color;       // 正文字形颜色（注释默认继承它）
    public int CharIndex;     // 字形在解析后纯文本中的字符下标
  }

  private readonly List<AnnotatedGlyph> _glyphs = new();

  /// <summary>
  /// 当前已记录的字形。RichTextLabel 每次重绘都会重新回调本效果追加记录，
  /// 由 Owner 在 _Draw() 消费后调用 Clear() 清空，避免跨帧累积。
  /// </summary>
  public IReadOnlyList<AnnotatedGlyph> Glyphs => _glyphs;

  /// <summary>持有本效果的 Label，由 LtcRichTextLabel 在 _Ready() 中注入。</summary>
  public LtcRichTextLabel Owner { get; set; }

  public void Clear() => _glyphs.Clear();

  public override bool _ProcessCustomFX(CharFXTransform charFX) {
    if (Owner == null) { return true; }
    // 跳过描边/阴影 pass。
    if (charFX.Outline) { return true; }
    // 引擎对 CJK 等字形会伴随一次 GlyphIndex=0（.notdef）的附加回调，过滤之。
    if (charFX.GlyphIndex == 0) { return true; }
    if (!charFX.Env.TryGetValue("ltc", out Variant value)) { return true; }

    string annotation = value.AsString().Trim('"');
    if (string.IsNullOrEmpty(annotation)) { return true; }

    float advance = 0f;
    if (charFX.Font.IsValid) {
      advance = TextServerManager.GetPrimaryInterface()
        .FontGetGlyphAdvance(charFX.Font, Owner.BaseFontSize, charFX.GlyphIndex).X;
    }
    if (advance <= 0f) { advance = Owner.BaseFontSize * 0.5f; } // 兜底

    // 注意：不可见字形（visible_ratio/visible_characters 截断）不会回调到这里，
    // 注释的整段显隐由 LtcRichTextLabel 依据 CharIndex 与 VisibleCharacters 判断。
    _glyphs.Add(new AnnotatedGlyph {
      Annotation = annotation,
      // charFX.Offset 是字形在整形缓冲内的偏移（通常恒为 0），
      // 真正的笔位在 Transform.Origin。
      Position = charFX.Transform.Origin,
      Advance = advance,
      Color = charFX.Color,
      CharIndex = charFX.Range.X,
    });
    return true; // 不修改字形本体，原样绘制
  }
}
