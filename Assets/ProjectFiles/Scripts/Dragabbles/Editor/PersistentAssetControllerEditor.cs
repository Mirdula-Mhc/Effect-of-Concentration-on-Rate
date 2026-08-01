#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(PersistentAssetController))]
public class PersistentAssetControllerEditor : Editor
{
    private SerializedProperty pageTransforms;
    private ReorderableList reorderableList;

    private void OnEnable()
    {
        pageTransforms = serializedObject.FindProperty("pageTransforms");

        reorderableList = new ReorderableList(
            serializedObject,
            pageTransforms,
            true,
            true,
            true,
            true);

        reorderableList.drawHeaderCallback = rect =>
        {
            EditorGUI.LabelField(rect, "Page Transforms");
        };

        reorderableList.elementHeightCallback = index =>
        {
            return EditorGUI.GetPropertyHeight(
                pageTransforms.GetArrayElementAtIndex(index), true) + 4;
        };

        reorderableList.drawElementCallback = (rect, index, isActive, isFocused) =>
        {
            rect.y += 2;

            SerializedProperty element =
                pageTransforms.GetArrayElementAtIndex(index);

            EditorGUI.PropertyField(
                rect,
                element,
                new GUIContent($"Page {element.FindPropertyRelative("pageIndex").intValue}"),
                true);
        };
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        reorderableList.DoLayoutList();

        GUILayout.Space(10);

        if (reorderableList.index >= 0 &&
            reorderableList.index < pageTransforms.arraySize)
        {
            SerializedProperty selected =
                pageTransforms.GetArrayElementAtIndex(reorderableList.index);

            int page =
                selected.FindPropertyRelative("pageIndex").intValue;

            EditorGUILayout.HelpBox(
                $"Selected Page : {page}",
                MessageType.Info);

            if (GUILayout.Button("Apply Current Transform"))
            {
                PersistentAssetController controller =
                    (PersistentAssetController)target;

                Undo.RecordObject(controller, "Apply Current Transform");

                selected.FindPropertyRelative("localPosition").vector3Value =
                    controller.transform.localPosition;

                selected.FindPropertyRelative("localEulerRotation").vector3Value =
                    controller.transform.localEulerAngles;

                selected.FindPropertyRelative("localScale").vector3Value =
                    controller.transform.localScale;

                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(controller);
            }
        }
        else
        {
            EditorGUILayout.HelpBox(
                "Select a Page Transform from the list above.",
                MessageType.Warning);
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif