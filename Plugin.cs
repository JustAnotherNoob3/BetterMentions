using SML;
using UnityEngine;
using HarmonyLib;
using System.Text.RegularExpressions;

using Server.Shared.State.Chat;
using System;
using Server.Shared.State;
using Services;
using Home.Shared;
using Game.Chat;
using TMPro;
using Mentions;
using Mentions.UI;

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
        [HarmonyPrefix]
        public static void Prefix(HudChatPoolItem __instance)
        {
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
                if ((highlight == "Mentions Highlights" || highlight == "Any Highlight") && msg != $"[[@{Pepper.GetMyPosition() + 1}]]")
                {
                    ((ChatItemData)__instance.Data).encodedText = ((ChatItemData)__instance.Data).encodedText.Replace("<color=white>", "<color=yellow>");
                    __instance.highlight = null;
                }
                goto ColorOthers;
            }
            bool highlightNum = ModSettings.GetBool("Non-Mentions Highlights", "JAN.bettermentions");
            bool highlightFully = ModSettings.GetBool("Highlight Non-Mention Message", "JAN.bettermentions");
            if (!highlightNum && !highlightFully) return;
            bool toReplace = false;
            ((ChatItemData)__instance.Data).encodedText = Regex.Replace(((ChatItemData)__instance.Data).encodedText, "(?<!\\[\\[:|\\[\\[@|\\[\\[#)\\b\\d{1,2}\\b(?!>|\">)", match =>
            {
                if (Convert.ToInt32(match.Value) != Pepper.GetMyPosition() + 1) return match.Value;
                if (highlightFully)
                {
                    if (highlight == "Non-Mentions Highlights" || highlight == "Any Highlight")
                    {
                        toReplace = true;
                        __instance.highlight = null;
                        return "<color=#FBCD3A>" + match.Value + "</color>";
                    }
                    else if (__instance.highlight != null)
                    {
                        __instance.highlight.gameObject.SetActive(true);
                        __instance.highlight = null;
                    }
                }
                if (highlightNum)
                    return "<color=#FBCD3A>" + match.Value + "</color>";
                else return match.Value;
            });
            if (toReplace) ((ChatItemData)__instance.Data).encodedText = ((ChatItemData)__instance.Data).encodedText.Replace("<color=white>", "<color=yellow>");
            ColorOthers:
            if (!ModSettings.GetBool("Other's number colored", "JAN.bettermentions")) return;
            if (Service.Game.Sim.simulation.m_currentGamePhase != GamePhase.PLAY) return;

            ((ChatItemData)__instance.Data).encodedText
            = Regex.Replace(((ChatItemData)__instance.Data).encodedText, "(?<!\\[\\[:|\\[\\[@|\\[\\[#)\\b\\d{1,2}\\b(?!>|\">|\"\\sname=\"Player)", match =>
            {
                int num = Convert.ToInt32(match.Value) - 1;
                if (num > Service.Game.Sim.simulation.validPlayerCount.Get() || num < 1 || num == Pepper.GetMyPosition()) return match.Value;
                Service.Game.Sim.simulation.knownRolesAndFactions.Data.TryGetValue(num, out Tuple<Role, FactionType> tuple);
                if (tuple == null) return match.Value;
                string color = "white";
                if (!(tuple.Item1 == Role.DEATH || tuple.Item1 == Role.HIDDEN))
                {
                    color = ClientRoleExtensions.GetFactionColor(tuple.Item2);
                }
                return $"<color={color}>{match.Value}</color>";
            });
        }
    }
    [HarmonyPatch(typeof(MentionMenuItem), "Initialize")]
    class MentionsPatchMenu
    {
        static public bool Prefix(MentionMenuItem __instance, MentionInfo mentionInfo)
        {
            if (mentionInfo.mentionInfoType != MentionInfo.MentionInfoType.PLAYER || Service.Game.Sim.simulation.m_currentGamePhase != GamePhase.PLAY || !ModSettings.GetBool("Mention Panel Colored")) return true;
            string p = mentionInfo.encodedText.Substring(3);
            string tempEncodedText = mentionInfo.richText;
            int num = Convert.ToInt32(p.Length == 3 ? p[0].ToString() : p.Substring(0, 2)) - 1;
            if (!ModSettings.GetBool("Other's Mentions colored", "JAN.bettermentions")) goto DeleteText;
            if (num > Service.Game.Sim.simulation.validPlayerCount.Get() - 1 || num < 0 || num == Pepper.GetMyPosition()) goto DeleteText;
            Service.Game.Sim.simulation.knownRolesAndFactions.Data.TryGetValue(num, out Tuple<Role, FactionType> tuple);
            if (tuple == null) goto DeleteText;
            string color = "white";
            if (!(tuple.Item1 == Role.DEATH || tuple.Item1 == Role.HIDDEN))
            {
                color = ClientRoleExtensions.GetFactionColor(tuple.Item2);
            }
            tempEncodedText = mentionInfo.richText.Replace("#FCCE3B", color);
            //mentionInfo.richText = tempEncodedText;
            DeleteText:
            /*if(Service.Home.UserService.Settings.MentionsPlayerEffects != 2 || !ModSettings.GetBool("Just show the numbers", "JAN.bettermentions")) goto SetText;
            string[] lol = mentionInfo.richText.Split(new string[]{"<color="}, StringSplitOptions.RemoveEmptyEntries);
            mentionInfo.richText = lol[0] + (lol[1].Split(new string[]{"</color>"}, StringSplitOptions.RemoveEmptyEntries)[1]);
            SetText:
            */
            // ! i need to figurate a way to make the game not delete the custom mentions
            __instance.mentionInfo = mentionInfo;
            __instance.textField.text = tempEncodedText;
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
            if ((flag1 | flag2) && Service.Game.Sim.simulation.m_currentGamePhase == GamePhase.PLAY)
                __result = Regex.Replace(__result, "(?<=<sprite=\"PlayerNumbers\"\\sname=\"PlayerNumbers_)\\d+\"><color=#[A-Za-z0-9]+>[A-Za-z0-9 ]+</color>", match =>
                {
                    if (flag1)
                    {
                        string p = match.Value.Substring(0, 3);
                        return p + (p[2] == '>' ? "" : ">");
                    }
                    if (flag2)
                    {
                        string p = match.Value.Substring(0, 2);
                        int num = Convert.ToInt32(p[1] == '"' ? p[0].ToString() : p) - 1;
                        if (num > Service.Game.Sim.simulation.validPlayerCount.Get() - 1 || num < 0 || num == Pepper.GetMyPosition()) return match.Value;
                        Service.Game.Sim.simulation.knownRolesAndFactions.Data.TryGetValue(num, out Tuple<Role, FactionType> tuple);
                        if (tuple == null) return match.Value;
                        string color = "white";
                        if (!(tuple.Item1 == Role.DEATH || tuple.Item1 == Role.HIDDEN))
                        {
                            color = ClientRoleExtensions.GetFactionColor(tuple.Item2);
                        }
                        
                        return $"{num + 1}\">{match.Value.Remove(0, (num < 9 ? 3 : 4)).Replace("<color=#FCCE3B>", $"<color={color}>")}";
                    }
                    Debug.LogWarning("what the fuck");
                    return match.Value;
                });
        }
    }
}