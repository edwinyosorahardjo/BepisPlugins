﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Harmony;
using Character;
using HarmonyLib;
using MessagePack;

namespace ExtensibleSaveFormat
{
    public partial class ExtendedSave
    {
        internal static class Hooks
        {
            internal static void InstallHooks()
            {
                HarmonyWrapper.PatchAll(typeof(Hooks));
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(CustomParameter), nameof(CustomParameter.Load), typeof(BinaryReader))]
            internal static void CustomParameterLoadPostHook(CustomParameter __instance, BinaryReader reader)
            {
                var dictionary = ReadExtData(reader) ?? new Dictionary<string, PluginData>();
                internalCharaDictionary.Set(__instance, dictionary);
                CardReadEvent(__instance);
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(CustomParameter), nameof(CustomParameter.Save), typeof(BinaryWriter))]
            internal static void CustomParameterLoadPostHook(CustomParameter __instance, BinaryWriter writer)
            {
                CardWriteEvent(__instance);
                var extendedData = GetAllExtendedData(__instance);
                WriteExtData(writer, extendedData);
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(CustomParameter), nameof(CustomParameter.LoadCoordinate), typeof(BinaryReader))]
            internal static void CustomParameterLoadCoordPostHook(CustomParameter __instance, BinaryReader reader)
            {
                var dictionary = ReadExtData(reader) ?? new Dictionary<string, PluginData>();
                internalCoordinateDictionary.Set(__instance, dictionary);
                CoordinateReadEvent(__instance);
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(CustomParameter), nameof(CustomParameter.SaveCoordinate), typeof(BinaryWriter))]
            internal static void CustomParameterLoadCoordPostHook(CustomParameter __instance, BinaryWriter writer)
            {
                CoordinateWriteEvent(__instance);
                var extendedData = GetAllExtendedCoordData(__instance);
                WriteExtData(writer, extendedData);
            }

            private static Dictionary<string, PluginData> ReadExtData(BinaryReader reader)
            {
                if (reader.BaseStream.Position != reader.BaseStream.Length)
                {
                    var originalPosition = reader.BaseStream.Position;

                    try
                    {
                        if (reader.PeekChar() == Marker[0] && reader.ReadString() == Marker)
                        {
                            var version = reader.ReadInt32();
                            if (version != DataVersion)
                                Logger.LogWarning("Unsupported version of extended data!");

                            var length = reader.ReadInt32();
                            if (length > 0)
                            {
                                var bytes = reader.ReadBytes(length);
                                var dictionary = MessagePackSerializer.Deserialize<Dictionary<string, PluginData>>(bytes);
                                Logger.LogDebug($"Read extended data count {dictionary.Count}");
                                return dictionary;
                            }
                        }
                    }
                    catch (EndOfStreamException)
                    {
                        //Incomplete/non-existant data
                    }
                    catch (SystemException)
                    {
                        //Invalid/unexpected deserialized data
                    }

                    // In case of a failure reset the stream so game can try reading the rest
                    reader.BaseStream.Position = originalPosition;
                }

                return null;
            }

            private static void WriteExtData(BinaryWriter writer, Dictionary<string, PluginData> extendedData)
            {
                if (extendedData == null || extendedData.Count(x => x.Value != null) == 0) return;

                var currentlySavingData = MessagePackSerializer.Serialize(extendedData);

                var length = currentlySavingData.LongLength;

                writer.Write(Marker);
                writer.Write(DataVersion);
                writer.Write(length);
                writer.Write(currentlySavingData);
            }
        }
    }
}