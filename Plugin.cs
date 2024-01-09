using SML;
using UnityEngine;
using HarmonyLib;
using System.Text.RegularExpressions;
using System.Linq;
using Server.Shared.State.Chat;
using System;
using Server.Shared.State;
using Services;
using Home.Shared;
using Game.Chat;
using TMPro;
using Mentions;
using Mentions.UI;
using Game.Interface;

namespace Main
{
    [Mod.SalemMod]
    public class Main
    {

        public static void Start()
        {
            Debug.Log("Working?");

        }
    }
    [HarmonyPatch(typeof(HudChatPoolItem), "Validate")]
    public class DetectNonMentions
    {
        [HarmonyPostfix]
        public static void Postfix(HudChatPoolItem __instance, ref bool forceRevalidate)
        {
            if (__instance.doInitialValidation)
            {
                __instance.doInitialValidation = false;
                forceRevalidate = true;
            }
            if (__instance.Data.Exists && !forceRevalidate) return;
            if (__instance.chatLogMessage == null) return;
            if (__instance.chatLogMessage.chatLogEntry == null) return;
            if (__instance.chatLogMessage.chatLogEntry.type != ChatType.CHAT) return;
            ChatLogChatMessageEntry messageEntry = (ChatLogChatMessageEntry)__instance.chatLogMessage.chatLogEntry;
            if (messageEntry.speakerId == ChatLogChatMessageEntry.JAILOR_SPEAKING_ID) return;
            if (!messageEntry.speakerWasAlive) return;
            string highlight = ModSettings.GetString("ToS 1 highlight", "JAN.bettermentions");
            string msg = messageEntry.message;
            if (msg.Contains($"[[@{Pepper.GetMyPosition() + 1}]]"))
            {
                if ((highlight == "Mentions Highlights" || highlight == "Any Highlight") && Regex.IsMatch(msg.Replace(" ", ""), @"(?<!\[\[(?:@|#|:))\w"))
                {
                    ((ChatItemData)__instance.Data).decodedText = ((ChatItemData)__instance.Data).decodedText.Replace("<color=white>", "<color=yellow>");
                    __instance.highlight.gameObject.SetActive(false);
                }

                goto ColorOthers;
            }
            bool highlightNum = ModSettings.GetBool("Non-Mentions Highlights", "JAN.bettermentions");
            bool highlightFully = ModSettings.GetBool("Highlight Non-Mention Message", "JAN.bettermentions");
            if (!highlightNum && !highlightFully) goto ColorOthers;
            bool toReplace = false;
            ((ChatItemData)__instance.Data).decodedText = Regex.Replace(((ChatItemData)__instance.Data).decodedText, "(?<!indent=)\\b\\d{1,2}\\b(?!>|\">)", match =>
            {
                if (Convert.ToInt32(match.Value) != Pepper.GetMyPosition() + 1) return match.Value;
                if (highlightFully)
                {
                    if (highlight == "Non-Mentions Highlights" || highlight == "Any Highlight")
                    {
                        toReplace = true;
                        return "<color=#FBCD3A>" + match.Value + "</color>";
                    }
                    else if (__instance.highlight != null)
                    {
                        __instance.highlight.gameObject.SetActive(true);
                    }
                }
                if (highlightNum)
                    return "<color=#FBCD3A>" + match.Value + "</color>";
                else return match.Value;
            });
            if (toReplace) ((ChatItemData)__instance.Data).decodedText = ((ChatItemData)__instance.Data).decodedText.Replace("<color=white>", "<color=yellow>");
            ColorOthers:
            if (!ModSettings.GetBool("Other's number colored", "JAN.bettermentions")) goto SetText;
            if (Service.Game.Sim.simulation.m_currentGamePhase != GamePhase.PLAY) goto SetText;
            bool s = !ModSettings.GetBool("Color My Number", "JAN.bettermentions");
            ((ChatItemData)__instance.Data).decodedText
            = Regex.Replace(((ChatItemData)__instance.Data).decodedText, "(?<!indent=)\\b\\d{1,2}\\b(?!>|\">|\"\\sname=\"Player)", match =>
            {
                int num = Convert.ToInt32(match.Value) - 1;
                if (num > Service.Game.Sim.simulation.validPlayerCount.Get() || num < 1 || (num == Pepper.GetMyPosition() && s)) return match.Value;
                Service.Game.Sim.simulation.knownRolesAndFactions.Data.TryGetValue(num, out Tuple<Role, FactionType> tuple);
                if (tuple == null) return match.Value;
                string color = "white";
                if (!(tuple.Item1 == Role.DEATH || tuple.Item1 == Role.HIDDEN || tuple.Item1 == Role.STONED))
                {
                    color = ClientRoleExtensions.GetFactionColor(tuple.Item2);
                }
                if (tuple.Item1 == Role.STONED) color = "#9C9A9A";
                return $"<color={color}>{match.Value}</color>";
            });
        SetText:
            __instance.textField.SetText(((ChatItemData)__instance.Data).decodedText);
        }
    }
    [HarmonyPatch(typeof(HudGraveyardPanel), "HandleOnKillRecordsChanged")]
    class UpdateMentions
    {
        static public void Postfix()
        {
            DontDeleteModdedMentions.update = true;
        }
    }
    [HarmonyPatch(typeof(MentionMenuItem), "Initialize")]
    class MentionsPatchMenu
    {
        static public bool Prefix(MentionMenuItem __instance, MentionInfo mentionInfo)
        {
            if (mentionInfo.mentionInfoType != MentionInfo.MentionInfoType.PLAYER || Service.Game.Sim.simulation.m_currentGamePhase != GamePhase.PLAY) return true;
            bool isColored = ModSettings.GetBool("Mention Panel Colored", "JAN.bettermentions");
            bool toInput = ModSettings.GetBool("Colored Input's Mentions", "JAN.bettermentions");
            if (!isColored && !toInput) return true;
            string p = mentionInfo.encodedText.Substring(3);
            int num = Convert.ToInt32(p.Length == 3 ? p[0].ToString() : p.Substring(0, 2)) - 1;
            string tempEncodedText = mentionInfo.richText;
            if (!isColored) __instance.textField.text = Regex.Replace(tempEncodedText, "<color=#[A-Za-z0-9]+>", match =>
            {
                return "<color=#FCCE3B>";
            });
            if (!ModSettings.GetBool("Other's Mentions colored", "JAN.bettermentions")) goto DeleteText;
            bool s = !ModSettings.GetBool("Color My Mention", "JAN.bettermentions");
            if (num == Pepper.GetMyPosition() && s) goto DeleteText;
            if (!tempEncodedText.Contains("<color=#FCCE3B>")) goto DeleteText;
            Service.Game.Sim.simulation.knownRolesAndFactions.Data.TryGetValue(num, out Tuple<Role, FactionType> tuple);
            if (tuple == null) goto DeleteText;
            string color = "white";
            if (!(tuple.Item1 == Role.DEATH || tuple.Item1 == Role.HIDDEN || tuple.Item1 == Role.STONED))
            {
                color = ClientRoleExtensions.GetFactionColor(tuple.Item2);
            }
            if (tuple.Item1 == Role.STONED) color = "#9C9A9A";
            tempEncodedText = mentionInfo.richText.Replace("#FCCE3B", color);
            if (toInput) mentionInfo.richText = tempEncodedText;
            DeleteText:
            //if(Service.Home.UserService.Settings.MentionsPlayerEffects != 2 || !ModSettings.GetBool("Just show the numbers", "JAN.bettermentions") || !toInput) goto SetText; i cant figure out a good way to do this, so it stays like this.
            //string[] lol = mentionInfo.richText.Split(new string[]{"<color="}, StringSplitOptions.RemoveEmptyEntries);
            //mentionInfo.richText = lol[0] + (lol[1].Split(new string[]{"</color>"}, StringSplitOptions.RemoveEmptyEntries)[1]);
            //SetText:
            if (toInput) __instance.mentionInfo = mentionInfo;
            if (isColored) __instance.textField.text = tempEncodedText;
            return false;
        }
    }
    [HarmonyPatch(typeof(MentionsProvider), "ProcessDecodedText")]
    class ProcessModdedText
    {
        [HarmonyPostfix]
        public static void Postfix(ref string __result)
        {
            __result = Regex.Replace(__result, "<sprite=\"PlayerNumbers\"\\sname=\"PlayerNumbers_(\\d+)\">(?:<color=#[A-Za-z0-9]+>[A-Za-z0-9 ]+</color>)?", match =>
            {
                return $"[[@{match.Groups[1].Value}]]";
            });
        }
    }

    [HarmonyPatch(typeof(MentionsProvider), "ValidateTextualMentions")]
    class DontDeleteModdedMentions
    {
        public static bool update = false;
        [HarmonyPrefix]
        public static bool Prefix(ref bool __result, MentionsProvider __instance)
        {
            bool result = false;
            if (Regex.IsMatch(__instance._matchInfo.fullText, "<sprite=\"PlayerNumbers\"\\sname=\"PlayerNumbers_\\d+\"><color=#[A-Za-z0-9]+>[A-Za-z0-9 ]+</color>"))
            {
                if (Service.Home.UserService.Settings.MentionsPlayerEffects == 2 && ModSettings.GetBool("Only Numbers in Inputs", "JAN.bettermentions"))
                {
                    __instance._matchInfo.fullText = Regex.Replace(__instance._matchInfo.fullText, "(<sprite=\"PlayerNumbers\"\\sname=\"PlayerNumbers_\\d+\">)<color=#[A-Za-z0-9]+>[A-Za-z0-9 ]+</color>", match =>
                    {
                        return match.Groups[1].Value;
                    });
                    result = true;
                }
                else if (ModSettings.GetBool("Colored Input's Mentions", "JAN.bettermentions") && update)
                {
                    __instance._matchInfo.fullText = Regex.Replace(__instance._matchInfo.fullText, "<sprite=\"PlayerNumbers\"\\sname=\"PlayerNumbers_(\\d+)\"><color=#FCCE3B>[A-Za-z0-9 ]+</color>", match =>
                    {
                        string text = match.Value;
                        int num = int.Parse(match.Groups[1].Value) - 1;
                        Service.Game.Sim.simulation.knownRolesAndFactions.Data.TryGetValue(num, out Tuple<Role, FactionType> tuple);
                        if (tuple == null) return text;
                        string color = "white";
                        if (!(tuple.Item1 == Role.DEATH || tuple.Item1 == Role.HIDDEN || tuple.Item1 == Role.STONED))
                        {
                            color = ClientRoleExtensions.GetFactionColor(tuple.Item2);
                        }
                        if (tuple.Item1 == Role.STONED) color = "#9C9A9A";
                        text = text.Replace("#FCCE3B", color);
                        return text;
                    });
                    update = false;
                    result = true;
                }
            }
            for (int j = 0; j < __instance._textualMentionInfos.Count; j++)
            {
                MentionInfo mentionInfo = __instance._textualMentionInfos[j];
                if (!__instance._matchInfo.fullText.Contains(mentionInfo.richText))
                {
                    __instance._textualMentionInfos.RemoveAt(j);
                    j--;
                }
            }
            int num = 0;
            for (; ; )
            {
                num = __instance._matchInfo.fullText.IndexOf(__instance.styleTagOpen, num);
                if (num < 0)
                {
                    __result = result;
                    return false;
                }
                int num2 = __instance._matchInfo.fullText.IndexOf(__instance.styleTagClose, num);
                if (num2 < 0)
                {
                    break;
                }
                string text = __instance._matchInfo.fullText.Substring(num, num2 - num + __instance.styleTagClose.Length);
                int fullHash = text.ToLower().GetHashCode();
                if (__instance.MentionInfos.Any((MentionInfo i) => i.hashCode == fullHash) || CheckIfValidMention(text))
                {
                    num++;
                }
                else
                {
                    __instance._matchInfo.fullText = __instance._matchInfo.fullText.Remove(num, text.Length);
                    result = true;
                    __instance._matchInfo.stringPosition = num;
                }
            }
            __instance._matchInfo.fullText = __instance._matchInfo.fullText.Substring(0, num);
            result = true;
            __result = result;
            return false;
        }
        static bool CheckIfValidMention(string text)
        {
            if (Regex.IsMatch(text, "<sprite=\"PlayerNumbers\"\\sname=\"PlayerNumbers_\\d+\"><color=#[A-Za-z0-9]+>[A-Za-z0-9 ]+</color>"))
            {
                Match match = Regex.Match(text, "<sprite=\"PlayerNumbers\"\\sname=\"PlayerNumbers_(\\d+)\"><color=#[A-Za-z0-9]+>([A-Za-z0-9 ]+)</color>");
                if (Service.Game.Sim.simulation.GetDisplayName(Convert.ToInt32(match.Groups[1].Value) - 1) == match.Groups[2].Value) return true;
                return false;
            }
            else if (Regex.IsMatch(text, "<sprite=\"PlayerNumbers\"\\sname=\"PlayerNumbers_\\d+\">(?!<color=#[A-Za-z0-9]+>([A-Za-z0-9 ]+)</color>)")) return true;
            return false;
        }

    }
    [HarmonyPatch(typeof(MentionsProvider), "DecodeText")]
    class MentionsPatchChat
    {
        [HarmonyPostfix]
        public static void Postfix(ref string __result, MentionsProvider __instance)
        {
            bool flag1 = Service.Home.UserService.Settings.MentionsPlayerEffects == 2 && ModSettings.GetBool("Just show the numbers", "JAN.bettermentions");
            bool flag2 = ModSettings.GetBool("Other's Mentions colored", "JAN.bettermentions");
            bool s = !ModSettings.GetBool("Color My Mention", "JAN.bettermentions");
            if ((flag1 | flag2) && Service.Game.Sim.simulation.m_currentGamePhase == GamePhase.PLAY)
                __result = Regex.Replace(__result, "(?<=<sprite=\"PlayerNumbers\"\\sname=\"PlayerNumbers_)(\\d+)\"><color=#[A-Za-z0-9]+>[A-Za-z0-9 ]+</color>", match =>
                {
                    if (flag1)
                    {
                        return match.Groups[1].Value + "\">";
                    }
                    if (flag2)
                    {
                        int num = Convert.ToInt32(match.Groups[1].Value) - 1;
                        if (num == Pepper.GetMyPosition() && s) return match.Value;
                        Service.Game.Sim.simulation.knownRolesAndFactions.Data.TryGetValue(num, out Tuple<Role, FactionType> tuple);
                        if (tuple == null) return match.Value;
                        string color = "white";
                        if (!(tuple.Item1 == Role.DEATH || tuple.Item1 == Role.HIDDEN || tuple.Item1 == Role.STONED))
                        {
                            color = ClientRoleExtensions.GetFactionColor(tuple.Item2);
                        }
                        if (tuple.Item1 == Role.STONED) color = "#9C9A9A";

                        return $"{num + 1}\">{match.Value.Remove(0, (num < 9 ? 3 : 4)).Replace("<color=#FCCE3B>", $"<color={color}>")}";
                    }
                    Debug.LogWarning("what the fuck");
                    return match.Value;
                });
        }
    }
}