// Magica Cloth - Editor helper to dispose Native collections before domain reload.
// Prevents "Native Collection has not been disposed" leaks when scripts recompile.
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace MagicaCloth
{
    [InitializeOnLoad]
    public static class MagicaClothNativeLeakFix
    {
        static MagicaClothNativeLeakFix()
        {
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
        }

        private static void OnBeforeAssemblyReload()
        {
            DisposeAllPhysicsManagers();
        }

        private static void DisposeAllPhysicsManagers()
        {
            MagicaPhysicsManager[] managers;
#if UNITY_2021_2_OR_NEWER
            managers = Object.FindObjectsByType<MagicaPhysicsManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            managers = Object.FindObjectsOfType<MagicaPhysicsManager>(true);
#endif
            foreach (var manager in managers)
            {
                if (manager == null) continue;
                try
                {
                    manager.Compute.Dispose();
                    manager.Wind.Dispose();
                    manager.Team.Dispose();
                    manager.Mesh.Dispose();
                    manager.Bone.Dispose();
                    manager.Particle.Dispose();
                    manager.Component.Dispose();
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[MagicaCloth] Native leak fix: Dispose failed for {manager.gameObject.name}: {ex.Message}");
                }
            }
        }
    }
}
#endif
