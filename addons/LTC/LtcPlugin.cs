using Godot;

/// <summary>
/// LTC 编辑器插件：把 LtcRichTextLabel / LtcAnnotationEffect
/// 注册进「创建节点 / 新建资源」对话框。
/// 注意：节点上直接挂脚本不依赖本插件启用，插件只提供编辑器集成。
/// </summary>
[Tool]
public partial class LtcPlugin : EditorPlugin {
  public override void _EnterTree() {
    AddCustomType("LtcRichTextLabel", "RichTextLabel",
      GD.Load<Script>("res://addons/LTC/LtcRichTextLabel.cs"), null);
    AddCustomType("LtcAnnotationEffect", "RichTextEffect",
      GD.Load<Script>("res://addons/LTC/LtcAnnotationEffect.cs"), null);
  }

  public override void _ExitTree() {
    RemoveCustomType("LtcRichTextLabel");
    RemoveCustomType("LtcAnnotationEffect");
  }
}
