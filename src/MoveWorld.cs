using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TerrainPatcher
{
    internal static class MoveWorld
    {
        public static Vector3 GLOBAL_OFFSET = new Vector3(0.0f, 0.0f, 0.0f);

        public static void Move(Vector3 by)
        {
            var player = Player.main.gameObject;
            var position = player.transform.position;
            var velocity = player.GetComponent<Rigidbody>().velocity;

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                foreach (var g in SceneManager.GetSceneAt(i).GetRootGameObjects())
                {
                    if (
                        g.name != "MainCamera (UI)" &&
                        g.name != "MainCamera" &&
                        !g.TryGetComponent<uGUI_BuilderMenu>(out var _)
                    )
                    {
                        Mod.LogInfo($"MOVING {g}");
                        g.transform.position += by;
                    }
                }
            }

            GLOBAL_OFFSET += by;

            UWE.CoroutineHost.StartCoroutine(SetPlayerNextFrame(position + by, velocity));

            IEnumerator<int> SetPlayerNextFrame(Vector3 position, Vector3 velocity)
            {
                yield return 0;

                var player = Player.main.gameObject;
                player.transform.position = position;
                player.GetComponent<Rigidbody>().velocity = velocity;

                yield break;
            }
        }
    }
}
