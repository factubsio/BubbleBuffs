using Owlcat.Runtime.UI.Controls.Button;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EidolonUI {
    public interface BubbleObject {
        public RectTransform Rect { get; }
        public Color Background { set;  }
    }
    public class TitleView : BubbleObject {
        public Color Background {
            set => bg.color = value;
        }

        private readonly Image bg;
        public RectTransform Rect => Obj.transform as RectTransform;

        public GameObject Obj { get; }
        public string Name {
            set => Obj.transform.Find("name").GetComponent<TextMeshProUGUI>().text = value;
        }
        public TitleView(GameObject prefab) {
            Obj = GameObject.Instantiate(prefab);
            bg = Obj.transform.Find("fill").GetComponent<Image>();
            image = Obj.transform.Find("icon").GetComponent<Image>();

        }
        private readonly Image image;
    }

    public class EvoToggle : BubbleObject {

        public Sprite Sprite {
            set {
                if (value != null) {
                    image.sprite = value;
                    image.enabled = true;
                } else {
                    image.enabled = false;
                }
            }
        }
        public Color Background {
            set => bg.color = value;
        }

        public RectTransform Rect => Obj.transform as RectTransform;

        public GameObject Obj { get; }
        public string Name {
            set => Obj.transform.Find("name").GetComponent<TextMeshProUGUI>().text = value;
        }

        public EvoToggle(GameObject prefab, Prefabs.ButtonSprites buttonSprites) {
            Obj = GameObject.Instantiate(prefab);
            bg = Obj.transform.Find("fill").GetComponent<Image>();
            image = Obj.transform.Find("icon").GetComponent<Image>();
            Button = Obj.GetComponentInChildren<OwlcatButton>();

            if (buttonSprites != null) {
                var frame = Obj.transform.Find("frame").GetComponent<Image>();
                frame.sprite = buttonSprites.Normal;
                frame.color = Color.white;

                Button.OnHover.AddListener(h => {
                    frame.sprite = h ? buttonSprites.Hover : buttonSprites.Normal;
                });
            }
        }

        private readonly Image bg;
        private readonly Image image;
        public readonly OwlcatButton Button;
    }
}
