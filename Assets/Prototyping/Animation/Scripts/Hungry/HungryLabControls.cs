using Televised.Prototyping.Shared;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Televised.Prototyping.AnimationLab
{
    /// <summary>
    /// Animation Lab panel for Hungry: pick expressions (buttons, Left/Right arrows), auto-cycle (Space) and toggle
    /// the procedural layers. H hides the panel. Expression values are tuned live on HungryFace in the Inspector.
    /// </summary>
    public class HungryLabControls : MonoBehaviour
    {
        [SerializeField] HungryFace face;
        [SerializeField] bool panelVisible = true;
        [SerializeField] bool autoCycle;
        [SerializeField, Min(0.5f)] float cycleSeconds = 2.5f;

        float _cycleTimer;
        Vector2 _scroll;
        GUIStyle _box, _title, _button, _selected;

        void Update()
        {
            if (face == null || face.Expressions.Count == 0) return;
            int n = face.Expressions.Count;
            if (PrototypeInput.Pressed(Key.H)) panelVisible = !panelVisible;
            if (PrototypeInput.Pressed(Key.Space)) autoCycle = !autoCycle;
            if (PrototypeInput.Pressed(Key.RightArrow)) Select((face.Current + 1) % n);
            if (PrototypeInput.Pressed(Key.LeftArrow)) Select((face.Current + n - 1) % n);
            if (autoCycle && (_cycleTimer -= Time.deltaTime) <= 0f) Select((face.Current + 1) % n);
        }

        void Select(int index)
        {
            face.SetExpression(index);
            _cycleTimer = cycleSeconds;
        }

        void OnGUI()
        {
            if (face == null) return;
            float scale = Mathf.Max(1f, Screen.height / 1080f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            EnsureStyles();

            if (!panelVisible)
            {
                GUI.Label(new Rect(12, 10, 400, 24), $"{face.CurrentName}   (H: panel)", _title);
                return;
            }

            GUILayout.BeginArea(new Rect(10, 10, 230, Screen.height / scale - 20), _box);
            GUILayout.Label("HUNGRY", _title);
            GUILayout.Label(face.CurrentName, _title);
            GUILayout.Space(4);
            _scroll = GUILayout.BeginScrollView(_scroll);
            for (int i = 0; i < face.Expressions.Count; i++)
                if (GUILayout.Button(face.Expressions[i].name, i == face.Current ? _selected : _button))
                    Select(i);
            GUILayout.EndScrollView();
            GUILayout.Space(6);
            autoCycle = GUILayout.Toggle(autoCycle, " Auto-cycle (Space)");
            face.lookAtMouse = GUILayout.Toggle(face.lookAtMouse, " Pupils follow mouse");
            face.wanderingPupils = GUILayout.Toggle(face.wanderingPupils, " Darting pupils");
            face.blinks = GUILayout.Toggle(face.blinks, " Blinks");
            face.browTwitches = GUILayout.Toggle(face.browTwitches, " Brow twitches");
            GUILayout.Label("←/→ cycle · H hide · tune values on HungryFace");
            GUILayout.EndArea();
        }

        void EnsureStyles()
        {
            if (_box != null) return;
            var bg = new Texture2D(1, 1);
            bg.SetPixel(0, 0, new Color(0.08f, 0.08f, 0.09f, 0.82f));
            bg.Apply();
            _box = new GUIStyle(GUI.skin.box) { padding = new RectOffset(10, 10, 10, 10) };
            _box.normal.background = bg;
            _title = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 15 };
            _title.normal.textColor = Color.white;
            _button = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft, fixedHeight = 24 };
            _selected = new GUIStyle(_button) { fontStyle = FontStyle.Bold };
            _selected.normal.textColor = _selected.hover.textColor = new Color(1f, 0.45f, 0.4f);
        }
    }
}
