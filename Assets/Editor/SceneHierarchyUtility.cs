using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>SampleSceneの編集用階層を一か所で管理する。</summary>
internal static class SceneHierarchyUtility
{
    internal const string CoreGroupName = "_Core";
    internal const string PresentationGroupName = "_Presentation";
    internal const string MinigamesGroupName = "_Minigames";
    internal const string WorldGroupName = "_World";

    internal static GameObject Find(Scene scene, string objectName)
    {
        return scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Select(transform => transform.gameObject)
            .FirstOrDefault(candidate => candidate.name == objectName);
    }

    internal static GameObject GetOrCreateGroup(Scene scene, string groupName)
    {
        GameObject group = scene.GetRootGameObjects().FirstOrDefault(root => root.name == groupName);
        if (group != null)
        {
            return group;
        }

        group = new GameObject(groupName);
        SceneManager.MoveGameObjectToScene(group, scene);
        return group;
    }

    internal static void MoveUnderGroup(Scene scene, GameObject target, string groupName)
    {
        if (target == null)
        {
            return;
        }

        Transform group = GetOrCreateGroup(scene, groupName).transform;
        if (target.transform.parent != group)
        {
            target.transform.SetParent(group, true);
        }
    }

    internal static void DestroyNamedObject(Scene scene, string objectName)
    {
        GameObject target = Find(scene, objectName);
        if (target != null)
        {
            Object.DestroyImmediate(target);
        }
    }
}
