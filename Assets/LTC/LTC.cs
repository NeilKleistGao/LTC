using UnityEngine;
using System.Text.RegularExpressions;
using TMPro;
using System;
using System.Text;
using System.Collections.Generic;

[RequireComponent(typeof(TMP_Text))]
public class LTC : MonoBehaviour {
  private struct TagInfo {
    public int startIndex;
    public int endIndex;
    public string topContent;
    public Vector2 offset;

    // TODO: support offset
    public TagInfo(int pStartIndex, int pEndIndex, string pTopContent) {
      startIndex = pStartIndex;
      endIndex = pEndIndex;
      topContent = pTopContent;
      offset = Vector2.zero;
    }
  }

  const string TAG_REGEX_PATTERN = @"<(/?[a-zA-Z0-9\s]+)(?:=(?<value>[^,<>]*)?(?:,(?<value>[^,<>]*)?)*)?>";

  private TMP_Text mText;
  private List<TagInfo> mTags = new List<TagInfo>();

  void Start() {
    mText = GetComponent<TMP_Text>();
    SetText(mText.text);
  }

  public void SetText(string pNewText) {
    mTags.Clear();
    MatchCollection collection = Regex.Matches(pNewText, TAG_REGEX_PATTERN);
    var resBuild = new StringBuilder();

    int curPos = 0;
    foreach (Match match in collection) {
      string tag = match.Value;
      int pos = match.Index;
      int length = pos - curPos;

      if (tag.StartsWith("<ltc")) {
        // TODO: properties?
      }
      else if (tag == "</ltc>") {
        string content = pNewText.Substring(curPos, length);
        mTags.Add(new TagInfo(curPos, curPos + length, content));
        curPos = pos + tag.Length;
        continue;
      }

      string sub = pNewText.Substring(curPos, length);
      resBuild.Append(sub);
      curPos = pos + tag.Length;
    }

    resBuild.Append(pNewText.Substring(curPos));
    mText.text = resBuild.ToString(); // TODO: how to render?
  }
}
