using System;
using System.IO;
using DesiLudo3D.Core;
using DesiLudo3D.Rules;
using UnityEngine;

namespace DesiLudo3D.Gameplay
{
    /// <summary>Auto-save / resume wiring (spec §4.3) on top of the
    /// hash-verified MatchRunner.Save()/Load(). One slot; atomic writes;
    /// deleted when the match ends. Device-only (spec §12).</summary>
    public class SaveService
    {
        readonly string _dir;
        readonly string _path;

        public SaveService()
        {
            _dir = Path.Combine(Application.persistentDataPath, "save");
            _path = Path.Combine(_dir, "match.txt");
        }

        public bool HasSave => File.Exists(_path);

        /// <summary>Auto-save after every completed event batch. If the app
        /// dies mid-animation, resume lands on the last completed batch —
        /// always a consistent, replay-verified state.</summary>
        public void Attach(PassAndPlayController controller)
        {
            controller.BatchCompleted += () =>
            {
                if (!controller.Runner.IsMatchOver) Save(controller.Runner);
            };
            controller.EventAnimated += e =>
            {
                if (e is MatchEnded) Delete();
            };
        }

        public void Save(MatchRunner runner)
        {
            try
            {
                Directory.CreateDirectory(_dir);
                string tmp = _path + ".tmp";
                File.WriteAllText(tmp, runner.Save());
                if (File.Exists(_path)) File.Delete(_path);
                File.Move(tmp, _path);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[DesiLudo3D] autosave failed: " + e.Message);
            }
        }

        /// <summary>The saved blob, or null.</summary>
        public string Load()
        {
            try
            {
                return HasSave ? File.ReadAllText(_path) : null;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[DesiLudo3D] save read failed: " + e.Message);
                return null;
            }
        }

        /// <summary>Restore a runner (integrity-checked replay inside) or
        /// null when the save is unusable. Fresh dice seed: recorded rolls
        /// are already explicit in the command log; post-resume rolls use
        /// fresh entropy (documented fairness note).</summary>
        public MatchRunner LoadRunner(int freshSeed)
        {
            string blob = Load();
            if (blob == null) return null;
            try
            {
                return MatchRunner.Load(blob, new SeededRandomDiceProvider(freshSeed));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[DesiLudo3D] save rejected: " + e.Message);
                return null;
            }
        }

        public void Delete()
        {
            try
            {
                if (File.Exists(_path)) File.Delete(_path);
            }
            catch (Exception) { }
        }
    }
}
