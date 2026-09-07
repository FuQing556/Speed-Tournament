using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace SpeedTournament
{
    public sealed partial class SpeedTournamentPrototype
    {
        public sealed partial class RaceHud
        {
            GameObject guidePanel;
            Text guideHeading,guideBody,guidePageNumber;
            ScrollRect guideScroll;
            Button guidePrevious,guideNext;
            readonly List<Button> guideTabs=new();
            readonly List<string> guideTitles=new(),guidePages=new();
            int guidePage;

            void BuildGuide(Transform parent)
            {
                ReadGuide();
                guidePanel=Panel("RaceGuide",parent,new Color(.005f,.008f,.03f,.97f),Vector2.zero,Vector2.one).gameObject;
                var card=Panel("Card",guidePanel.transform,Color.white,new Vector2(.075f,.055f),new Vector2(.925f,.945f));
                UsePanelArt(card);
                var title=Label("Title",card.transform,"驾驶手册",48,TextAnchor.MiddleCenter,Color.white);
                SetAnchors(title.rectTransform,new Vector2(.10f,.84f),new Vector2(.9f,.96f));
                for(int i=0;i<guideTitles.Count;i++)
                {
                    int page=i;float x=.085f+i*.212f;
                    var tab=ButtonRect("GuideTab_"+i,card.transform,guideTitles[i],Color.white,()=>SelectGuidePage(page));
                    UsePillArt(tab.GetComponent<Image>());tab.transform.Find("Glyph").GetComponent<Text>().fontSize=28;
                    SetAnchors(tab.GetComponent<RectTransform>(),new Vector2(x,.727f),new Vector2(x+.195f,.818f));
                    guideTabs.Add(tab);
                }
                var reading=Panel("ReadingArea",card.transform,new Color(.018f,.035f,.090f,.95f),new Vector2(.087f,.215f),new Vector2(.913f,.711f));
                guideHeading=Label("SectionTitle",reading.transform,"",30,TextAnchor.MiddleLeft,RaceVisuals.Cyan);
                SetAnchors(guideHeading.rectTransform,new Vector2(.035f,.855f),new Vector2(.46f,.985f));
                var hint=Label("ScrollHint",reading.transform,"上下滑动阅读",21,TextAnchor.MiddleRight,new Color(.65f,.79f,.93f));
                SetAnchors(hint.rectTransform,new Vector2(.50f,.855f),new Vector2(.95f,.985f));
                var scrollRoot=Panel("Scroll",reading.transform,Color.clear,new Vector2(.032f,.035f),new Vector2(.965f,.84f));
                guideScroll=scrollRoot.gameObject.AddComponent<ScrollRect>();guideScroll.horizontal=false;guideScroll.vertical=true;
                guideScroll.movementType=ScrollRect.MovementType.Clamped;guideScroll.scrollSensitivity=35f;guideScroll.decelerationRate=.12f;
                var viewport=Panel("Viewport",scrollRoot.transform,Color.clear,Vector2.zero,new Vector2(.97f,1f));
                viewport.gameObject.AddComponent<RectMask2D>();guideScroll.viewport=viewport.rectTransform;
                guideBody=Label("Content",viewport.transform,"",28,TextAnchor.UpperLeft,new Color(.88f,.94f,1f));
                guideBody.horizontalOverflow=HorizontalWrapMode.Wrap;guideBody.verticalOverflow=VerticalWrapMode.Overflow;
                guideBody.lineSpacing=1.23f;guideBody.supportRichText=true;
                var rt=guideBody.rectTransform;rt.anchorMin=new Vector2(0,1);rt.anchorMax=Vector2.one;rt.pivot=new Vector2(.5f,1);rt.offsetMin=rt.offsetMax=Vector2.zero;
                guideBody.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
                guideScroll.content=rt;
                var bar=Panel("ScrollBar",scrollRoot.transform,new Color(.12f,.19f,.31f),new Vector2(.985f,0),Vector2.one);
                var scrollbar=bar.gameObject.AddComponent<Scrollbar>();scrollbar.direction=Scrollbar.Direction.BottomToTop;
                var handle=Panel("Handle",bar.transform,RaceVisuals.Cyan,Vector2.zero,Vector2.one);
                scrollbar.handleRect=handle.rectTransform;scrollbar.targetGraphic=handle;
                guideScroll.verticalScrollbar=scrollbar;guideScroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
                guidePrevious=GuideFooterButton(card.transform,"PreviousPage","上一页",.09f,.26f,()=>SelectGuidePage(guidePage-1));
                guideNext=GuideFooterButton(card.transform,"NextPage","下一页",.28f,.45f,()=>SelectGuidePage(guidePage+1));
                guidePageNumber=Label("PageNumber",card.transform,"",23,TextAnchor.MiddleCenter,new Color(.66f,.84f,1f));
                SetAnchors(guidePageNumber.rectTransform,new Vector2(.46f,.12f),new Vector2(.66f,.19f));
                GuideFooterButton(card.transform,"CloseGuide","返回选角",.68f,.90f,()=>TryCloseGuide());
                guidePanel.SetActive(false);
            }
            Button GuideFooterButton(Transform parent,string name,string label,float left,float right,UnityEngine.Events.UnityAction action)
            {
                var b=ButtonRect(name,parent,label,Color.white,action);UsePillArt(b.GetComponent<Image>());
                b.transform.Find("Glyph").GetComponent<Text>().fontSize=28;
                SetAnchors(b.GetComponent<RectTransform>(),new Vector2(left,.119f),new Vector2(right,.206f));return b;
            }
            void ReadGuide()
            {
                var source=Resources.Load<TextAsset>("Docs/RaceGuide");
                string text=source!=null?source.text:"# 基础操作\n自动前进，左右切轨；能量充足即可释放技能。";
                text=text.Replace("{{基础速度}}",RaceBalance.BaseSpeed.ToString("0"))
                    .Replace("{{技能回复}}",(RaceBalance.PassiveSkill*100).ToString("0.#"))
                    .Replace("{{氮气回复}}",(RaceBalance.PassiveNitro*100).ToString("0.#"))
                    .Replace("{{橙瓶}}",(RaceBalance.PickupSkill*100).ToString("0"))
                    .Replace("{{蓝瓶}}",(RaceBalance.PickupNitro*100).ToString("0"))
                    .Replace("{{氮气消耗}}",(RaceBalance.NitroCost*100).ToString("0"))
                    .Replace("{{氮气时长}}",RaceBalance.NitroDuration.ToString("0.#"))
                    .Replace("{{氮气提速}}",(RaceBalance.NitroBonus*100).ToString("0"))
                    .Replace("{{起步提前}}",RaceBalance.LaunchEarlyWindow.ToString("0.00"))
                    .Replace("{{起步延后}}",RaceBalance.LaunchLateWindow.ToString("0.00"))
                    .Replace("{{起步提速}}",(RaceBalance.LaunchBonus*100).ToString("0"))
                    .Replace("{{起步时长}}",RaceBalance.LaunchDuration.ToString("0.0"));
                for(int i=0;i<CharacterNames.Length;i++)text=text.Replace("{{角色"+i+"}}",RaceBalance.Descriptions[i]);
                var body=new StringBuilder();string heading=null;
                foreach(string raw in text.Replace("\r","").Split('\n'))
                {
                    if(raw.StartsWith("# "))
                    {
                        if(heading!=null){guideTitles.Add(heading);guidePages.Add(body.ToString().Trim());body.Clear();}
                        heading=raw.Substring(2).Trim();continue;
                    }
                    if(raw.StartsWith("## "))body.AppendLine("<color=#62E5FF><b>"+raw.Substring(3)+"</b></color>");
                    else body.AppendLine(raw.StartsWith("- ")?"• "+raw.Substring(2):raw);
                }
                if(heading!=null){guideTitles.Add(heading);guidePages.Add(body.ToString().Trim());}
            }
            public void ShowGuide()
            {
                if(!game.IsChoosingCharacter||!characterPanel.activeSelf)return;
                guidePanel.SetActive(true);SelectGuidePage(0);
            }
            public void SelectGuidePage(int index)
            {
                guidePage=Mathf.Clamp(index,0,guidePages.Count-1);
                guideHeading.text=guideTitles[guidePage];guideBody.text=guidePages[guidePage];
                guidePageNumber.text=(guidePage+1)+" / "+guidePages.Count;
                guidePrevious.interactable=guidePage>0;guideNext.interactable=guidePage<guidePages.Count-1;
                for(int i=0;i<guideTabs.Count;i++)guideTabs[i].GetComponent<Image>().color=i==guidePage?Color.white:new Color(.48f,.57f,.73f,.9f);
                Canvas.ForceUpdateCanvases();LayoutRebuilder.ForceRebuildLayoutImmediate(guideBody.rectTransform);
                guideScroll.StopMovement();guideScroll.verticalNormalizedPosition=1;
                game.Sound?.Play(RaceSound.Cue.Click);
            }
            public bool TryCloseGuide()
            {
                if(guidePanel==null||!guidePanel.activeInHierarchy)return false;
                guideScroll.StopMovement();guidePanel.SetActive(false);game.Sound?.Play(RaceSound.Cue.Click);return true;
            }
        }
    }
}
