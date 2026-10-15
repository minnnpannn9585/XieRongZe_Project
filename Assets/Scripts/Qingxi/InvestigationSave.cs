using System;
using UnityEngine;

namespace Qingxi
{
    /// <summary>Stores actions, never trusted balances or clue text. Replays only valid saves.</summary>
    public static class InvestigationSave
    {
        [Serializable]
        sealed class Snapshot
        {
            public int version = 1;
            public string chapter;
            public int[] actions;
        }

        static string Key(int index) => "Qingxi.Investigation." + index;

        public static string Encode(InvestigationSession session)
        {
            var data = new Snapshot { chapter = session.Case.ChapterName, actions = new int[session.Clues.Count] };
            for (int i = 0; i < data.actions.Length; i++)
                data.actions[i] = session.Clues[i].Area * 2 + (session.Clues[i].IsKey ? 1 : 0);
            return JsonUtility.ToJson(data);
        }

        public static bool TryDecode(CaseDefinition definition, string json, out InvestigationSession restored)
        {
            restored = null;
            if (string.IsNullOrEmpty(json)) return false;
            try
            {
                Snapshot data = JsonUtility.FromJson<Snapshot>(json);
                if (data == null || data.version != 1 || data.chapter != definition.ChapterName ||
                    data.actions == null || data.actions.Length > definition.Areas.Length * 2)
                    return false;
                var candidate = new InvestigationSession(definition);
                foreach (int action in data.actions)
                {
                    if (action < 0 || action >= definition.Areas.Length * 2) return false;
                    bool accepted = action % 2 == 0
                        ? candidate.TrySurface(action / 2, out _)
                        : candidate.TryDeep(action / 2, out _);
                    if (!accepted) return false;
                }
                restored = candidate;
                return true;
            }
            catch (ArgumentException) { return false; }
        }

        public static bool TryLoad(int index, out InvestigationSession restored) =>
            TryDecode(QingxiCases.Get(index), PlayerPrefs.GetString(Key(index), ""), out restored);

        public static void Store(int index, InvestigationSession session)
        {
            PlayerPrefs.SetString(Key(index), Encode(session));
            PlayerPrefs.Save();
        }

        public static void Clear(int index)
        {
            PlayerPrefs.DeleteKey(Key(index));
            PlayerPrefs.Save();
        }
    }
}
