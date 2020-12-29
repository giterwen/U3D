using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Xml;

/// <summary>
/// 文本控件，支持超链接、图片
/// </summary>

namespace UI.UGUIExtension
{
    public class LinkImageTextPro : Text, IPointerClickHandler
    {
        private string no_breaking_space = "\u00A0";
        /// <summary>
        /// 解析完最终的文本
        /// </summary>
        private string m_OutputText;

        /// <summary>
        /// 图片
        /// </summary>
        public Image m_Image;

        /// <summary>
        /// 超链接信息列表
        /// </summary>
        private readonly List<HrefInfo> m_HrefInfos = new List<HrefInfo>();

        /// <summary>
        /// 文本构造器
        /// </summary>
        protected static readonly StringBuilder s_TextBuilder = new StringBuilder();

        /// <summary>
        /// 正则取出所需要的属性
        /// </summary>
        private static readonly Regex s_ImageRegex =
            new Regex("<image name=\"(.+?)\" jump_id=\"(\\d{1,4})\" jump_url=\"(\\S+)\">(</image>)", RegexOptions.Singleline);

        /// <summary>
        /// 超链接正则
        /// </summary>
        private static readonly Regex s_HrefRegex =
            new Regex("<a href=\"([^>\n\\s]+?)\">(.*?)(</a>)", RegexOptions.Singleline);

        private int link_text_type = 1;
        private bool isHaveImage = false;

        public override void SetVerticesDirty()
        {
            base.SetVerticesDirty();
            UpdateImage();
        }

        public void SetLinkedText(String str)
        {
            isHaveImage = false;
            text = str;
            SetVerticesDirty();
        }
        public String GetLinkedText()
        {
            return text;
        }

        public int GetLinkedTextType()
        {
            return link_text_type;
        }

        protected void UpdateImage()
        {
#if UNITY_EDITOR
            if (UnityEditor.PrefabUtility.GetPrefabType(this) == UnityEditor.PrefabType.Prefab)
            {
                return;
            }
#endif
            m_OutputText = GetOutputText(text);
            foreach (Match match in s_ImageRegex.Matches(m_OutputText))
            {
                isHaveImage = true;
                if (m_Image != null)
                {
                    var spriteName = match.Groups[1].Value;
                    var jump_id = int.Parse(match.Groups[2].Value);
                    var jump_url = match.Groups[3].Value;
                    if (m_Image.sprite == null || m_Image.sprite.name != spriteName)
                    {
                        m_Image.sprite = Framework.GameApplication.Get().Resources.Load(spriteName, typeof(Sprite)) as Sprite;
                    }
                    Button button = m_Image.GetComponent<Button>();
                    object[] parameters = new object[2];
                    parameters[0] = jump_id;
                    parameters[1] = jump_url;
                    if (button != null)
                    {
                        button.onClick.RemoveAllListeners();
                        button.onClick.AddListener(()=>
                        {
                            Framework.GameApplication.Get().Events.Raise("events.banner.ClickLinkImage", parameters);
                        });
                    }
                }
            }
            if (isHaveImage)
            {
                if (m_OutputText.EndsWith("</image>"))
                {
                    link_text_type = 2;
                }
                else
                {
                    link_text_type = 3;
                }
            }
            else
            {
                link_text_type = 1;
            }
            m_OutputText = s_ImageRegex.Replace(m_OutputText, "");
            m_OutputText = m_OutputText.Replace(" ", no_breaking_space);
        }

        protected override void OnPopulateMesh(VertexHelper toFill)
        {
            var orignText = m_Text;
            m_Text = m_OutputText;
            base.OnPopulateMesh(toFill);
            m_Text = orignText;

            UIVertex vert = new UIVertex();
            // 处理超链接包围框
            foreach (var hrefInfo in m_HrefInfos)
            {
                hrefInfo.boxes.Clear();
                if (hrefInfo.startIndex >= toFill.currentVertCount)
                {
                    continue;
                }

                // 将超链接里面的文本顶点索引坐标加入到包围框
                toFill.PopulateUIVertex(ref vert, hrefInfo.startIndex);
                var pos = vert.position;
                var bounds = new Bounds(pos, Vector3.zero);
                for (int i = hrefInfo.startIndex, m = hrefInfo.endIndex; i < m; i++)
                {
                    if (i >= toFill.currentVertCount)
                    {
                        break;
                    }

                    toFill.PopulateUIVertex(ref vert, i);
                    pos = vert.position;
                    if (pos.x < bounds.min.x) // 换行重新添加包围框
                    {
                        hrefInfo.boxes.Add(new Rect(bounds.min, bounds.size));
                        bounds = new Bounds(pos, Vector3.zero);
                    }
                    else
                    {
                        bounds.Encapsulate(pos); // 扩展包围框
                    }
                }
                hrefInfo.boxes.Add(new Rect(bounds.min, bounds.size));
            }
        }

        /// <summary>
        /// 获取超链接解析后的最后输出文本
        /// </summary>
        /// <returns></returns>
        protected virtual string GetOutputText(string outputText)
        {
            outputText = ParseXML(outputText);
            s_TextBuilder.Length = 0;
            m_HrefInfos.Clear();
            var indexText = 0;
            foreach (Match match in s_HrefRegex.Matches(outputText))
            {
                s_TextBuilder.Append(outputText.Substring(indexText, match.Index - indexText));
                s_TextBuilder.Append("<color=blue>");  // 超链接颜色

                var group = match.Groups[1];
                var hrefInfo = new HrefInfo
                {
                    startIndex = s_TextBuilder.Length * 4, // 超链接里的文本起始顶点索引
                    endIndex = (s_TextBuilder.Length + match.Groups[2].Length - 1) * 4 + 3,
                    name = group.Value
                };
                m_HrefInfos.Add(hrefInfo);

                s_TextBuilder.Append(match.Groups[2].Value);
                s_TextBuilder.Append("</color>");
                indexText = match.Index + match.Length;
            }
            s_TextBuilder.Append(outputText.Substring(indexText, outputText.Length - indexText));
            return s_TextBuilder.ToString();
        }

        ///将标准HTML转换为RichText
        protected string ParseXML(string content)
        {
            content = "<root>" + content + "</root>";
            StringBuilder builder = new StringBuilder();
            XmlDocument xmlDocument = new XmlDocument();
            xmlDocument.LoadXml(content);
            XmlNode rootNode = xmlDocument.SelectSingleNode("root");
            XmlNodeList p_nodeList = rootNode.SelectNodes("p");
            foreach (XmlNode node in p_nodeList)
            {
                foreach (XmlNode child in node.ChildNodes)
                {
                    if (child.NodeType == XmlNodeType.Text)
                    {
                        builder.Append(child.InnerText);
                    }
                    else if (child.NodeType == XmlNodeType.Element)
                    {
                        builder.Append(ParseElement(child));
                    }
                }
                builder.Append("\n");
            }
            return builder.ToString();
        }

        ///迭代解析XmlElement
        protected string ParseElement(XmlNode node)
        {
            StringBuilder builder = new StringBuilder();
            string endStr = "";
            switch (node.Name)
            {
                case "em":
                    builder.Append("<i>");
                    endStr = endStr + "</i>";
                    break;
                case "strong":
                    builder.Append("<b>");
                    endStr = endStr + "</b>";
                    break;
                case "span":
                    string attribute = ((XmlElement)node).GetAttribute("style");
                    string[] infos = attribute.Split(':');
                    if (infos[0] == "font-size")
                    {
                        builder.Append("<size=" + infos[1].Replace("px", ">"));
                        endStr = "</size>" + endStr;
                    }
                    else if (infos[0] == "color")
                    {
                        builder.Append("<color=" + infos[1] + ">");
                        endStr = "</color>" + endStr;
                    }
                    break;
                case "a":
                    builder.Append(node.OuterXml);
                    break;
                case "image":
                    builder.Append(node.OuterXml);
                    break;
                default:
                    break;
            }
            foreach (XmlNode item in node.ChildNodes)
            {
                if (item.NodeType == XmlNodeType.Text && node.Name != "a")
                {
                    builder.Append(item.InnerText);
                }
                else if (item.NodeType == XmlNodeType.Element)
                {
                    builder.Append(ParseElement(item));
                }
            }
            builder.Append(endStr);
            return builder.ToString();
        }

        /// <summary>
        /// 点击事件检测是否点击到超链接文本
        /// </summary>
        /// <param name="eventData"></param>
        public void OnPointerClick(PointerEventData eventData)
        {
            Vector2 lp;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform, eventData.position, eventData.pressEventCamera, out lp);

            foreach (var hrefInfo in m_HrefInfos)
            {
                var boxes = hrefInfo.boxes;
                for (var i = 0; i < boxes.Count; ++i)
                {
                    if (boxes[i].Contains(lp))
                    {
                        Framework.GameApplication.Get().Events.Raise("events.banner.ClickHref", hrefInfo.name);
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// 超链接信息类
        /// </summary>
        private class HrefInfo
        {
            public int startIndex;

            public int endIndex;

            public string name;

            public readonly List<Rect> boxes = new List<Rect>();
        }
    }
}