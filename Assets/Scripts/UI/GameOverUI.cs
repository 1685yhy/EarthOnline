using UnityEngine;

namespace StackingCute
{
    public class GameOverUI : MonoBehaviour
    {
        [SerializeField] private float _reviveTimeLimit = 15f;
        private GameManager _gm; private float _rt; private bool _ra = true;

        private void Awake() { _gm = FindObjectOfType<GameManager>(); }
        private void OnEnable() { _rt = _reviveTimeLimit; _ra = true; }

        private void OnGUI()
        {
            if (!_gm || _gm.CurrentState != GameState.Over) return;
            float sw = Screen.width, sh = Screen.height;
            GUI.color = new Color(0, 0, 0, 0.6f);
            GUI.DrawTexture(new Rect(0, 0, sw, sh), Texture2D.whiteTexture);
            GUI.color = Color.white;
            float pw = sw * 0.8f, ph = sh * 0.55f, px = (sw - pw) / 2, py = sh * 0.2f;
            GUI.Box(new Rect(px, py, pw, ph), "");

            var ts = new GUIStyle(GUI.skin.label) { fontSize = 36, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            bool cl = _gm.CurrentLevelConfig && _gm.CurrentLayer >= _gm.CurrentLevelConfig.TargetLayers;
            GUI.Label(new Rect(px, py + 20, pw, 50), cl ? "通关!" : "再试一次!", ts);

            var ss = new GUIStyle(GUI.skin.label) { fontSize = 72, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            ss.normal.textColor = new Color(1f, 0.85f, 0.24f);
            GUI.Label(new Rect(px, py + 80, pw, 80), _gm.CurrentScore.ToString(), ss);

            var rs = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(px, py + 160, pw, 30), "历史最高: " + _gm.BestRecord + "层", rs);
            GUI.Label(new Rect(px, py + 190, pw, 25), "本局: " + _gm.CurrentLayer + "层 | " + _gm.CurrentGold + "金币", rs);

            float bw = pw * 0.7f, bh = 50, bx = (sw - bw) / 2, by = py + ph - 160;
            if (_ra) { _rt -= Time.unscaledDeltaTime; if (_rt <= 0) _ra = false; }
            if (_ra) { GUI.backgroundColor = new Color(1f, 0.85f, 0.24f); var rbs = new GUIStyle(GUI.skin.button) { fontSize = 22, fontStyle = FontStyle.Bold }; rbs.normal.textColor = Color.white; if (GUI.Button(new Rect(bx, by, bw, bh), "看广告续命 (" + _rt.ToString("F0") + "s)", rbs)) Revive(); by += bh + 10; }
            GUI.backgroundColor = new Color(1f, 0.9f, 0.7f);
            if (GUI.Button(new Rect(bx, by, bw, bh), "金币续命 (20金币)")) { if (_gm.CurrentGold >= 20) { _gm.CurrentGold -= 20; Revive(); } }
            by += bh + 10;
            GUI.backgroundColor = Color.white;
            if (GUI.Button(new Rect(bx, by, bw, bh), "直接重试")) { _gm.RestartGame(); _rt = _reviveTimeLimit; _ra = true; }
        }

        private void Revive()
        {
            _gm.CurrentCombo = 0; _gm.CurrentTowerWidth = 1f; _gm.LastOverlapRatio = 0f; _gm.SetState(GameState.Playing);
            var tm = FindObjectOfType<TowerManager>(); if (tm) tm.SpawnFirstBlock();
            _rt = _reviveTimeLimit; _ra = false;
        }
    }
}