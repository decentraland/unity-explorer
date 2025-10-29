#if UNITY_2018_2_OR_NEWER
#define TMP_WEBGL_SUPPORT
#endif

#if TMP_WEBGL_SUPPORT
using UnityEngine;
using TMPro;
using WebGLSupport.Detail;
using UnityEngine.UI;
using System;

namespace WebGLSupport
{
    /// <summary>
    ///     Wrapper for TMPro.TMP_InputField
    /// </summary>
    internal class WrappedTMPInputField : IInputField
    {
        private readonly TMP_InputField input;
        private readonly RebuildChecker checker;
        private Coroutine delayedGraphicRebuild;

        public bool ReadOnly => input.readOnly;

        public string text
        {
            get => input.text;
            set => input.text = FixContentTypeByInputField(value);
        }

        /// <summary>
        ///     workaround!!
        ///     when use TMP_InputField.text = "xxx"; is will set the text directly.
        ///     so, use InputField for match the ContentType!
        /// </summary>
        /// <param name="inText"></param>
        /// <returns></returns>
        private string FixContentTypeByInputField(string inText)
        {
            var go = new GameObject("FixContentTypeByInputField for WebGLInput");
            go.SetActive(false);
            InputField i = go.AddComponent<InputField>();
            i.contentType = (InputField.ContentType)Enum.Parse(typeof(InputField.ContentType), input.contentType.ToString());
            i.lineType = (InputField.LineType)Enum.Parse(typeof(InputField.LineType), input.lineType.ToString());
            i.inputType = (InputField.InputType)Enum.Parse(typeof(InputField.InputType), input.inputType.ToString());
            i.keyboardType = input.keyboardType;
            i.characterValidation = (InputField.CharacterValidation)Enum.Parse(typeof(InputField.CharacterValidation), input.characterValidation.ToString());
            i.characterLimit = input.characterLimit;
            i.text = inText;
            string res = i.text;
            GameObject.Destroy(go);
            return res;
        }

        public string placeholder
        {
            get
            {
                if (!input.placeholder)
                    return "";

                TMP_Text text = input.placeholder.GetComponent<TMP_Text>();
                return text ? text.text : "";
            }
        }

        public int fontSize => (int)input.textComponent.fontSize;

        public ContentType contentType => (ContentType)input.contentType;

        public LineType lineType => (LineType)input.lineType;

        public int characterLimit => input.characterLimit;

        public int caretPosition => input.caretPosition;

        public bool isFocused => input.isFocused;

        public int selectionFocusPosition
        {
            get => input.selectionStringFocusPosition;
            set => input.selectionStringFocusPosition = value;
        }

        public int selectionAnchorPosition
        {
            get => input.selectionStringAnchorPosition;
            set => input.selectionStringAnchorPosition = value;
        }

        public bool OnFocusSelectAll => input.onFocusSelectAll;

        public bool EnableMobileSupport
        {
            get
            {
                // [2023.2] Latest Development on TextMesh Pro
                // https://forum.unity.com/threads/2023-2-latest-development-on-textmesh-pro.1434757/
                // As of 2023.2, the TextMesh Pro package (com.unity.textmeshpro) has been merged into the uGUI package (com.unity.ugui) and the TextMesh Pro package has been deprecated.
                // In this version, TextMeshPro is default support mobile input. so disable WebGLInput mobile support
#if UNITY_2023_2_OR_NEWER

                // return false to use unity mobile keyboard support
                return false;
#else
                return true;
#endif
            }
        }

        public WrappedTMPInputField(TMP_InputField input)
        {
            this.input = input;
            checker = new RebuildChecker(this);
        }

        public Rect GetScreenCoordinates() =>

            // 表示範囲
            // MEMO :
            //  TMP では textComponent を移動させてクリッピングするため、
            //  表示範囲外になる場合があるので、自分の範囲を返す
            Support.GetScreenCoordinates(input.GetComponent<RectTransform>());

        public void ActivateInputField()
        {
            input.ActivateInputField();
        }

        public void DeactivateInputField()
        {
            input.DeactivateInputField();
        }

        public void Rebuild()
        {
#if UNITY_2020_1_OR_NEWER
            if (checker.NeedRebuild())
            {
                input.textComponent.SetVerticesDirty();
                input.textComponent.SetLayoutDirty();
                input.Rebuild(CanvasUpdate.LatePreRender);
            }
#else
            if (input.textComponent.enabled && checker.NeedRebuild())
            {
                //================================
                // fix bug for tmp
                // TMPの不具合で、正しく座標を設定されてなかったため、試しに対応する
                var rt = input.textComponent.GetComponent<RectTransform>();
                var size = input.textComponent.GetPreferredValues();
                if (size.x < rt.rect.xMax)
                {
                    // textComponent の座標を更新
                    var pos = rt.anchoredPosition;
                    pos.x = 0;
                    rt.anchoredPosition = pos;

                    // caret の座標更新
                    var caret = input.GetComponentInChildren<TMP_SelectionCaret>();
                    var caretRect = caret.GetComponent<RectTransform>();
                    caretRect.anchoredPosition = rt.anchoredPosition;
                }
                //==============================

                // HACK : 1フレーム無効にする
                // MEMO : 他にいい方法Rebuildがあれば対応する
                // LayoutRebuilder.ForceRebuildLayoutImmediate(); で試してダメでした
                input.textComponent.enabled = rectOverlaps(input.textComponent.rectTransform, input.textViewport);
                input.textComponent.SetAllDirty();
                input.Rebuild(CanvasUpdate.LatePreRender);
                //Debug.Log(input.textComponent.enabled);
            }
            else
            {
                input.textComponent.enabled = true;
            }
#endif
        }

        private bool rectOverlaps(RectTransform rectTrans1, RectTransform rectTrans2)
        {
            var rect1 = new Rect(rectTrans1.localPosition.x, rectTrans1.localPosition.y, rectTrans1.rect.width, rectTrans1.rect.height);
            var rect2 = new Rect(rectTrans2.localPosition.x, rectTrans2.localPosition.y, rectTrans2.rect.width, rectTrans2.rect.height);

            return rect1.Overlaps(rect2);
        }
    }
}

#endif // TMP_WEBGL_SUPPORT
