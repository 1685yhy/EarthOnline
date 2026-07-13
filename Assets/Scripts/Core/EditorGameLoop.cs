using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace StackingCute
{
    [ExecuteAlways]
    public class EditorGameLoop : MonoBehaviour
    {
        [SerializeField] private GameManager _gameManager;
        [SerializeField] private BlockController _blockController;
#if UNITY_EDITOR
        private void OnEnable() { EditorApplication.update += Tick; }
        private void OnDisable() { EditorApplication.update -= Tick; }
        private void Tick() { if (Application.isPlaying && _gameManager && _blockController && _gameManager.CurrentState == GameState.Playing) _blockController.Tick(); }
#endif
    }
}