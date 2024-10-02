using SML;
using UnityEngine;
using UnityEngine.UI;
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
using Mentions.Providers;
using System.Collections;
using System.Collections.Generic;
using System.Reflection.Emit;

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
    [ConditionalPatch("curtis.tuba.better.tos2", true)]
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
            string highlightColor = ModSettings.GetString("ToS 1 Highlight Color", "JAN.bettermentions");
            string baseMentionColor = ModSettings.GetString("Base Mentions Color", "JAN.bettermentions");
            string msg = messageEntry.message;
            if (msg.Contains($"[[@{Pepper.GetMyPosition() + 1}]]"))
            {
                if ((highlight == "Mentions Highlights" || highlight == "Any Highlight") && Regex.IsMatch(msg.Replace(" ", ""), @"(?<!\[\[(?:@|#|:))\w"))
                {
                    ((ChatItemData)__instance.Data).decodedText = ((ChatItemData)__instance.Data).decodedText.Replace("<color=white>", $"<color={highlightColor}>");
                    if(!ModSettings.GetBool("ToS 2 Highlight W ToS 1's","JAN.bettermentions"))__instance.highlight.gameObject.SetActive(false);
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
                        if(ModSettings.GetBool("ToS 2 Highlight W ToS 1's","JAN.bettermentions")) __instance.highlight.gameObject.SetActive(true);
                        return $"<color={baseMentionColor}>" + match.Value + "</color>";
                    }
                    else if (__instance.highlight != null)
                    {
                        __instance.highlight.gameObject.SetActive(true);
                    }
                }
                if (highlightNum)
                    return $"<color={baseMentionColor}>" + match.Value + "</color>";
                else return match.Value;
            });
            if (toReplace) ((ChatItemData)__instance.Data).decodedText = ((ChatItemData)__instance.Data).decodedText.Replace("<color=white>", $"<color={highlightColor}>");
            ColorOthers:
            if (!ModSettings.GetBool("Other's number colored", "JAN.bettermentions")) goto SetText;
            if (Service.Game.Sim.simulation.m_currentGamePhase != GamePhase.PLAY) goto SetText;
            bool s = !ModSettings.GetBool("Color My Number", "JAN.bettermentions");
            ((ChatItemData)__instance.Data).decodedText
            = Regex.Replace(((ChatItemData)__instance.Data).decodedText, "(?<!indent=)\\b\\d{1,2}\\b(?!>|\">|\"\\sname=\"Player)", match =>
            {
                int num = Convert.ToInt32(match.Value) - 1;
                if (num > Service.Game.Sim.simulation.validPlayerCount.Get() || num < 0 || (num == Pepper.GetMyPosition() && s)) return match.Value;
                Service.Game.Sim.simulation.knownRolesAndFactions.Data.TryGetValue(num, out Tuple<Role, FactionType> tuple);
                if (tuple == null) return match.Value;
                string color = baseMentionColor;
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
    [ConditionalPatch("curtis.tuba.better.tos2", true)]
    [HarmonyPatch(typeof(MentionMenuItem), "Initialize")]
    class MentionsPatchMenu
    {
        static public bool Prefix(MentionMenuItem __instance, MentionInfo mentionInfo)
        {
            if (mentionInfo.mentionInfoType != MentionInfo.MentionInfoType.PLAYER || Service.Game.Sim.simulation.m_currentGamePhase != GamePhase.PLAY) return true;
            bool isColored = ModSettings.GetBool("Mention Panel Colored", "JAN.bettermentions");
            bool toInput = ModSettings.GetBool("Colored Input's Mentions", "JAN.bettermentions");
            if (!isColored && !toInput) return true;
            string baseColor = ModSettings.GetString("Base Mentions Color", "JAN.bettermentions");
            string p = mentionInfo.encodedText.Substring(3);
            int num = Convert.ToInt32(p.Length == 3 ? p[0].ToString() : p.Substring(0, 2)) - 1;
            string tempEncodedText = mentionInfo.richText;
            if (!isColored) __instance.textField.text = Regex.Replace(tempEncodedText, "<color=#[A-Za-z0-9]+>", match =>
            {
                return $"<color={baseColor}>";
            });
            if (!ModSettings.GetBool("Other's Mentions colored", "JAN.bettermentions")) goto DeleteText;
            bool s = !ModSettings.GetBool("Color My Mention", "JAN.bettermentions");
            if (num == Pepper.GetMyPosition() && s) goto DeleteText;
            if (!tempEncodedText.Contains($"<color={baseColor}>")) goto DeleteText;
            Service.Game.Sim.simulation.knownRolesAndFactions.Data.TryGetValue(num, out Tuple<Role, FactionType> tuple);
            if (tuple == null) goto DeleteText;
            string color = baseColor;
            if (!(tuple.Item1 == Role.DEATH || tuple.Item1 == Role.HIDDEN || tuple.Item1 == Role.STONED))
            {
                color = ClientRoleExtensions.GetFactionColor(tuple.Item2);
            }
            else if (tuple.Item1 == Role.STONED) color = "#9C9A9A";
            tempEncodedText = mentionInfo.richText.Replace(baseColor, color);
            if (toInput) mentionInfo.richText = tempEncodedText;
            DeleteText:
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
            __result = Regex.Replace(__result, "<link=\"(\\d+)\">(?:<sprite=\"PlayerNumbers\"\\sname=\"PlayerNumbers_\\d+\">|<sprite=\"Cast\" name=\"Skin\\d+\">)?(?:<color=#[A-Za-z0-9]+>[A-Za-z0-9 <>=#]+</color>)?", match =>
            {
                return $"[[@{int.Parse(match.Groups[1].Value) + 1}]]";
            });
        }
    }
    [ConditionalPatch("curtis.tuba.better.tos2", true)]
    [HarmonyPatch(typeof(MentionsProvider), "ValidateTextualMentions")]
    class DontDeleteModdedMentions
    {
        public static bool update = false;
        [HarmonyPrefix]
        public static bool Prefix(ref bool __result, MentionsProvider __instance)
        {
            bool result = false;
            string baseColor = ModSettings.GetString("Base Mentions Color", "JAN.bettermentions");
            bool numbesrs = Service.Home.UserService.Settings.MentionsPlayerEffects == 2 && ModSettings.GetBool("Only Numbers in Inputs", "JAN.bettermentions");
            bool coloredInput = ModSettings.GetBool("Colored Input's Mentions", "JAN.bettermentions") && update;
            if(numbesrs || coloredInput)
            if (Regex.IsMatch(__instance._matchInfo.fullText, "<link=\"(\\d+)\">(?:<sprite=\"PlayerNumbers\"\\sname=\"PlayerNumbers_\\d+\">|<sprite=\"Cast\" name=\"Skin\\d+\">)?<color=#[A-Za-z0-9]+>[A-Za-z0-9 ]+</color>"))
            {
                if (numbesrs)
                {
                    __instance._matchInfo.fullText = Regex.Replace(__instance._matchInfo.fullText, "(<sprite=\"PlayerNumbers\"\\sname=\"PlayerNumbers_\\d+\">)<color=#[A-Za-z0-9]+>[A-Za-z0-9 ]+</color>", match =>
                    {
                        return match.Groups[1].Value;
                    });
                    result = true;
                }
                else if (coloredInput)
                {
                    __instance._matchInfo.fullText = Regex.Replace(__instance._matchInfo.fullText, "<link=\"(\\d+)\">(?:<sprite=\"PlayerNumbers\"\\sname=\"PlayerNumbers_\\d+\">|<sprite=\"Cast\" name=\"Skin\\d+\">)?<color=#FCCE3B>[A-Za-z0-9 ]+</color>", match =>
                    {
                        string text = match.Value;
                        int num = int.Parse(match.Groups[1].Value);
                        Service.Game.Sim.simulation.knownRolesAndFactions.Data.TryGetValue(num, out Tuple<Role, FactionType> tuple);
                        if (tuple == null) return text;
                        string color = baseColor;
                        if (!(tuple.Item1 == Role.DEATH || tuple.Item1 == Role.HIDDEN || tuple.Item1 == Role.STONED))
                        {
                            color = ClientRoleExtensions.GetFactionColor(tuple.Item2);
                        }
                        if (tuple.Item1 == Role.STONED) color = "#9C9A9A";
                        text = text.Replace(baseColor, color);
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
            if (Regex.IsMatch(text, "<link=\"(\\d+)\">(?:<sprite=\"PlayerNumbers\"\\sname=\"PlayerNumbers_\\d+\">|<sprite=\"Cast\" name=\"Skin\\d+\">)?<color=#[A-Za-z0-9]+>[A-Za-z0-9 ]+</color>"))
            {
                Match match = Regex.Match(text, "<link=\"(\\d+)\">(?:<sprite=\"PlayerNumbers\"\\sname=\"PlayerNumbers_\\d+\">|<sprite=\"Cast\" name=\"Skin\\d+\">)?<color=#[A-Za-z0-9]+>([A-Za-z0-9 ]+)</color>");
                if (Service.Game.Sim.simulation.GetDisplayName(Convert.ToInt32(match.Groups[1].Value)) == match.Groups[2].Value) return true;
                return false;
            }
            else if (Regex.IsMatch(text, "<sprite=\"PlayerNumbers\"\\sname=\"PlayerNumbers_\\d+\">(?!<color=#[A-Za-z0-9]+>([A-Za-z0-9 ]+)</color>)")) return true;
            return false;
        }

    }
    [ConditionalPatch("curtis.tuba.better.tos2", true)]
    [HarmonyPatch(typeof(MentionsProvider), "DecodeText")]
    class MentionsPatchChat
    {
        [HarmonyPostfix]
        public static void Postfix(ref string __result, MentionsProvider __instance)
        {
            bool flag1 = Service.Home.UserService.Settings.MentionsPlayerEffects == 2 && ModSettings.GetBool("Just show the numbers", "JAN.bettermentions");
            bool flag2 = ModSettings.GetBool("Other's Mentions colored", "JAN.bettermentions");
            bool s = !ModSettings.GetBool("Color My Mention", "JAN.bettermentions");
            if ((flag1 || flag2) && Service.Game.Sim.simulation.m_currentGamePhase == GamePhase.PLAY){
                string baseColor = $"<color={ModSettings.GetString("Base Mentions Color", "JAN.bettermentions")}>";
                __result = Regex.Replace(__result, "<link=\"(\\d+)\">(?:<sprite=\"PlayerNumbers\"\\sname=\"PlayerNumbers_\\d+\">|<sprite=\"Cast\" name=\"Skin\\d+\">)?<color=#[A-Za-z0-9]+>[A-Za-z0-9 ]+</color>", match =>
                {
                    if (flag1)
                    {
                        return $"<link=\"{match.Groups[1].Value}\"><sprite=\"PlayerNumbers\" name=\"PlayerNumbers_{int.Parse(match.Groups[1].Value) + 1}\">";
                    }
                    if (flag2)
                    {
                        int num = Convert.ToInt32(match.Groups[1].Value);
                        if (num == Pepper.GetMyPosition() && s) return match.Value;
                        Service.Game.Sim.simulation.knownRolesAndFactions.Data.TryGetValue(num, out Tuple<Role, FactionType> tuple);
                        if (tuple == null) return match.Value;
                        string color = baseColor;
                        if (!(tuple.Item1 == Role.DEATH || tuple.Item1 == Role.HIDDEN || tuple.Item1 == Role.STONED))
                        {
                            color = ClientRoleExtensions.GetFactionColor(tuple.Item2);
                        }
                        if (tuple.Item1 == Role.STONED) color = "#9C9A9A";

                        return match.Value.Replace(baseColor, $"<color={color}>");
                    }
                    Debug.LogWarning("what the fuck");
                    return match.Value;
                });
        }
        }
    }
    [HarmonyPatch(typeof(ChatItemFactory), "Start")]
    public static class AddCustomClass
    {
        static public void Prefix(ChatItemFactory __instance)
        {
            Color color = ModSettings.GetColor("ToS 2 Highlight Color", "JAN.bettermentions");
            color.a = ModSettings.GetFloat("Highlight Opacity", "JAN.bettermentions");
            Image img = __instance.ChatItemTemplate.transform.Find("Highlight").GetComponent<Image>();
            img.color = color;
        }
    }
    [HarmonyPatch(typeof(SharedMentionsProvider), "PreparePlayerMentions")]
    class PreparePlayerMentionsColor
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Ldstr && codes[i].operand is string str && str == "<color=#FCCE3B>")
                {
                    try{
                    string color = ModSettings.GetString("Base Mentions Color", "JAN.bettermentions");
                    codes[i].operand = $"<color={color}>";
                    } catch {
                        Console.WriteLine("First time launching Better Player Mentions.");
                    }
                }
            }
            return codes.AsEnumerable();
        }
    }
}