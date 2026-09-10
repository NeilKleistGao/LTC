using Godot;
using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>
/// 支持 [ltc="注释"]正文[/ltc] 的 RichTextLabel。
/// 在 _Ready() 中自动安装 LtcAnnotationEffect 并为注释腾出顶部空间，
/// 在 _Draw() 中以小一号字体把注释绘制到正文上方（居中于被注释的文字段）。
/// </summary>
[GlobalClass, Tool]
public partial class LtcRichTextLabel : RichTextLabel {
  /// <summary>注释字号；0 = 自动（正文字号的 55%）。</summary>
  [Export] public int AnnotationFontSize { get; set; } = 0;

  /// <summary>注释颜色；Alpha 为 0 时继承正文颜色。</summary>
  [Export] public Color AnnotationColor { get; set; } = new(0, 0, 0, 0);

  /// <summary>注释与正文之间的像素间距。</summary>
  [Export] public float AnnotationGap { get; set; } = 2f;

  /// <summary>自动字号相对正文字号的比例。</summary>
  [Export] public float AnnotationScale { get; set; } = 0.55f;

  public int BaseFontSize => GetThemeFontSize("normal_font_size");

  // 引擎对自定义 RichTextEffect 标签不支持 [ltc="X"] 简写（识别名会连带参数），
  // 因此解析前将其改写为等价的 [ltc ltc="X"] 选项形式。
  private static readonly Regex LtcTagPattern =
    new Regex(@"\[ltc=(?:""([^""]*)""|([^\]\s]+))\]", RegexOptions.Compiled);

  /// <summary>把 [ltc="X"] / [ltc=X] 改写为引擎可识别的 [ltc ltc="X"] 形式（幂等）。</summary>
  public static string PreprocessLtcTags(string source) {
    if (string.IsNullOrEmpty(source) || !source.Contains("[ltc=")) { return source; }
    return LtcTagPattern.Replace(source, m =>
      $"[ltc ltc=\"{(m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value)}\"]");
  }

  private LtcAnnotationEffect _effect;

  public override void _Ready() {
    base._Ready();
    EnsureEffect();
    RefreshParse();
    ApplyAnnotationMargin();
  }

  public override bool _Set(StringName property, Variant value) {
    // 文本在运行时被重新赋值时，延迟一帧重新做标签改写与解析（_Ready 已覆盖初始加载）。
    if (property == PropertyName.Text && IsNodeReady()) { CallDeferred(MethodName.RefreshParse); }
    return base._Set(property, value);
  }

  private void RefreshParse() {
    if (!BbcodeEnabled || string.IsNullOrEmpty(Text)) { return; }
    // 注意：引擎的 ParseBbcode/AppendText 不会回写 text 属性，
    // 因此段区间必须基于本地算出的改写结果，而不是重新读取 Text。
    string transformed = PreprocessLtcTags(Text);
    if (transformed != Text) {
      // 场景加载时 [ltc] 尚未被识别，visible_ratio 是按含标签原文的总长
      // 折算成 visible_characters 的；重新解析后总长变短，必须按场景中的
      // 赋值顺序（先 visible_characters 后 visible_ratio）重新应用一遍。
      int visChars = VisibleCharacters;
      float visRatio = VisibleRatio;
      ParseBbcode(transformed);
      if (visChars >= 0) { VisibleCharacters = visChars; }
      if (visRatio < 1f) {
        // set_visible_ratio 在数值不变时直接返回（不会重算），
        // 这里按重新解析后的总长手动折算。
        VisibleCharacters = Mathf.FloorToInt(GetTotalCharacterCount() * visRatio);
      }
    }
    RebuildSegments(transformed);
  }

  public override void _Notification(int what) {
    base._Notification(what);
    if (what == NotificationThemeChanged && IsNodeReady()) {
      ApplyAnnotationMargin(); // 主题变化后重新确认注释空间（幂等）
    }
  }

  private void EnsureEffect() {
    var effects = CustomEffects ?? new Godot.Collections.Array();
    // 复用已有的效果实例；清理重复实例（[Tool] 脚本在编辑器里挂效果，
    // 场景保存后再次加载时容易累积出多个）。
    var deduped = new Godot.Collections.Array();
    bool changed = false;
    foreach (Variant v in effects) {
      if (v.AsGodotObject() is LtcAnnotationEffect existing) {
        if (_effect == null) { _effect = existing; deduped.Add(v); }
        else { changed = true; } // 丢弃重复的 ltc 效果
      } else {
        deduped.Add(v);
      }
    }
    if (_effect == null) {
      _effect = new LtcAnnotationEffect();
      deduped.Add(_effect);
      changed = true;
    }
    if (changed) { CustomEffects = deduped; }
    _effect.Owner = this;
  }

  private int GetAnnotationSize() {
    if (AnnotationFontSize > 0) { return AnnotationFontSize; }
    return Mathf.Max(8, Mathf.RoundToInt(BaseFontSize * AnnotationScale));
  }

  /// <summary>
  /// 通过 normal StyleBox 的顶部 ContentMargin 为注释腾出空间。
  /// 幂等：顶部空间已足够时不再叠加（[Tool] 脚本在编辑器里保存场景后
  /// 重新加载，避免边距/最大尺寸逐次累积）。
  /// </summary>
  private void ApplyAnnotationMargin() {
    if (!BbcodeEnabled || !(Text?.Contains("[ltc") ?? false)) { return; }

    Font font = GetThemeFont("normal_font");
    float needed = Mathf.Ceil(font.GetHeight(GetAnnotationSize()) + AnnotationGap);

    StyleBox current = GetThemeStylebox("normal");
    if (current.ContentMarginTop < needed) {
      StyleBox sb = (StyleBox)(current?.Duplicate() ?? new StyleBoxEmpty());
      sb.ContentMarginTop = (current?.ContentMarginTop ?? 0f) + needed;
      AddThemeStyleboxOverride("normal", sb);
    }

    // custom_maximum_size 会钳制 get_combined_minimum_size 的结果，
    // 顶部边距会把内容高度推高，必须同步补偿，否则正文底部被裁剪。
    if (CustomMaximumSize.Y > 0
        && CustomMaximumSize.Y - CustomMinimumSize.Y < needed) {
      CustomMaximumSize = new Vector2(CustomMaximumSize.X, CustomMinimumSize.Y + needed);
    }

    UpdateMinimumSize();
    QueueRedraw();
  }

  /// <summary>一段 [ltc] 标注在解析后纯文本中的字符区间。</summary>
  private struct LtcSegment {
    public string Annotation;
    public string PlainText; // 段内正文的纯文本（已剥离嵌套标签）
    public int Start, End;
  }

  private readonly List<LtcSegment> _segments = new();

  // 作用于改写后的文本（[ltc ltc="X"]base[/ltc] 形式）。
  private static readonly Regex SegmentPattern = new Regex(
    @"\[ltc ltc=""([^""]*)""\](.*?)\[/ltc\]", RegexOptions.Compiled | RegexOptions.Singleline);
  private static readonly Regex AnyTagPattern = new Regex(@"\[[^\]]*\]", RegexOptions.Compiled);

  private static int PlainLength(string s) => AnyTagPattern.Replace(s, "").Length;

  /// <summary>从改写后的文本中提取所有标注段的纯文本字符区间。</summary>
  private void RebuildSegments(string src) {
    _segments.Clear();
    foreach (Match m in SegmentPattern.Matches(src)) {
      int start = PlainLength(src.Substring(0, m.Index));
      string plain = AnyTagPattern.Replace(m.Groups[2].Value, "");
      _segments.Add(new LtcSegment {
        Annotation = m.Groups[1].Value, PlainText = plain, Start = start, End = start + plain.Length,
      });
    }
  }

  private struct Run {
    public string Annotation;
    public float StartX, EndX, Y;
    public Color Color;
    public int CharIndex; // 段内第一个可见字形的字符下标
  }

  /// <summary>把相邻同注释、同基线的字形合并为一段，每段画一次注释。</summary>
  private List<Run> BuildRuns() {
    var runs = new List<Run>();
    foreach (var g in _effect.Glyphs) {
      if (runs.Count > 0) {
        Run last = runs[^1];
        bool sameLine = Mathf.Abs(g.Position.Y - last.Y) < 1f;
        bool contiguous = g.Position.X - last.EndX < 4f; // 同一行且基本相邻
        if (sameLine && contiguous && last.Annotation == g.Annotation) {
          last.EndX = g.Position.X + g.Advance;
          runs[^1] = last;
          continue;
        }
      }
      runs.Add(new Run {
        Annotation = g.Annotation,
        StartX = g.Position.X,
        EndX = g.Position.X + g.Advance,
        Y = g.Position.Y,
        Color = g.Color,
        CharIndex = g.CharIndex,
      });
    }
    return runs;
  }

  public override void _Draw() {
    if (_effect == null) { return; }
    // 脚本 _Draw 在原生文本绘制（含效果回调）之后执行，
    // 此处消费本轮记录的字形坐标并清空，防止跨重绘累积。
    var glyphs = _effect.Glyphs;
    if (glyphs.Count > 0) {
      Font font = GetThemeFont("normal_font");
      int baseSize = BaseFontSize;
      int annSize = GetAnnotationSize();
      float baseAscent = font.GetAscent(baseSize);
      Rid canvas = GetCanvasItem();

      int visChars = VisibleCharacters; // visible_ratio 会被引擎换算进该值；-1 表示全部可见
      foreach (Run run in BuildRuns()) {
        LtcSegment? found = null;
        foreach (LtcSegment s in _segments) {
          if (run.CharIndex >= s.Start && run.CharIndex < s.End) { found = s; break; }
        }
        if (found == null) { continue; }
        LtcSegment seg = found.Value;
        int segLen = seg.End - seg.Start;
        if (segLen <= 0) { continue; }

        // 注释跟随正文的揭示进度：正文揭示到段内第 n 个字符，
        // 注释也只显示前 floor(n / 段长 * 注释长) 个字符（逐字跟随）；
        // 段完全显示时注释才完整出现。被截断的字形不会回调到效果里，
        // 因此 run 内的字形即该段当前可见的前缀。
        int nVis = visChars < 0 ? segLen : Mathf.Clamp(visChars - seg.Start, 0, segLen);
        if (nVis <= 0) { continue; }

        float annWidth = font.GetStringSize(run.Annotation, fontSize: annSize).X;
        // 段总宽 = 可见前缀宽（实测）+ 未显示后缀宽（按主题字体测量）。
        float segWidth = run.EndX - run.StartX;
        if (nVis < segLen) {
          segWidth += font.GetStringSize(seg.PlainText.Substring(nVis), fontSize: baseSize).X;
        }
        float x = run.StartX + (segWidth - annWidth) * 0.5f; // 水平居中于整段
        // 字形 Position.Y 是正文基线：注释基线 = 正文基线 - 正文上高 - 间距，
        // 配合 ApplyAnnotationMargin 腾出的顶部空间，注释恰好落在正文上方。
        float y = run.Y - baseAscent - AnnotationGap;
        Color color = AnnotationColor.A > 0f ? AnnotationColor : run.Color;

        int k = nVis >= segLen
          ? run.Annotation.Length
          : Mathf.Clamp(Mathf.FloorToInt((float)nVis / segLen * run.Annotation.Length), 0, run.Annotation.Length);
        if (k <= 0) { continue; }
        string shown = k >= run.Annotation.Length ? run.Annotation : run.Annotation.Substring(0, k);
        font.DrawString(canvas, new Vector2(x, y), shown, fontSize: annSize, modulate: color);
      }
    }
    _effect.Clear();
  }
}
