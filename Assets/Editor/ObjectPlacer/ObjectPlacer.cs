// Created by Ryan Pocock @autumnpioneer
// Feel free to follow me on twitter or donate via ko-fi :)
// https://ko-fi.com/autumnpioneer

using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System.Collections.Generic;
 
public class ObjectPlacer : EditorWindow
{

    // Add window to Window, open with Ctrl + Shift + T
    [MenuItem("Window/Object Placer _%#T")]
    public static void ShowWindow()
    {
        // Open and focus the window
        var window = GetWindow<ObjectPlacer>();

        // Set start size, hacky but works
        window.minSize = new Vector2(290,480);
        window.maxSize = new Vector2(290,480);

        // Set actual min, max size
        window.minSize = new Vector2(290,300);
        window.maxSize = new Vector2(4000,4000);

        // Add a title to the window
        window.titleContent = new GUIContent("Object Placer");
        window.titleContent.image = Resources.Load<Texture2D>("OP_Icon");
    }

    private void OnEnable()
    {
        // Rerence to the root of the window
        var root = this.rootVisualElement;

        // Assign a stylesheet to the root and its children
        root.styleSheets.Add(Resources.Load<StyleSheet>("ObjectPlacer"));

        // Loads and clones our VisualTree (Our UXML structure) inside the root
        var ObjectPlacerVisualTree = Resources.Load<VisualTreeAsset>("ObjectPlacer");
        ObjectPlacerVisualTree.CloneTree(root);

        // Change selection zone to material
        root.Q<ObjectField>("GameObject-Picker").objectType = typeof(GameObject);

        // Change selection zone to scene object
        root.Q<ObjectField>("Parent-Picker").objectType = typeof(GameObject);

        // Add function to duringSceneGUI tick
        SceneView.duringSceneGui += SceneViewUpdate;
    }

    void OnDestroy()
    {
        // Remove the function to stop it runing
        SceneView.duringSceneGui -= SceneViewUpdate;
    }

    // Detect click events in scene
    void SceneViewUpdate(SceneView sceneView)
    {
        // Make a reference to the current event
        var e = Event.current;
        Toggle enabledToggle = (Toggle)this.rootVisualElement.Q<Toggle>("Enabled-Toggle");
        ObjectField objectSelector = (ObjectField)this.rootVisualElement.Q<ObjectField>("GameObject-Picker");

        // If the button is left click and down
        if (e.type == EventType.MouseDown && e.button == 0 
            && enabledToggle.value == true && objectSelector.value != null
            && !e.alt)
        {
            // Run function with the game object that is
            InstantiateGameObject((GameObject)objectSelector.value);
        }

        // If the object is placed, deselect it
        if (e.type == EventType.Used
            && enabledToggle.value == true && objectSelector.value != null
            && !e.alt)
        {
            // Deselect
            Selection.activeGameObject = null;
        }
    }

    void InstantiateGameObject(GameObject go)
    {
        // Get User Options
        string name = (string)this.rootVisualElement.Q<TextField>("Name-Field").value;
        bool nameToggle = (bool)this.rootVisualElement.Q<Toggle>("Name-Toggle").value;
        bool staticToggle = (bool)this.rootVisualElement.Q<Toggle>("Static-Toggle").value;
        GameObject parent = (GameObject)this.rootVisualElement.Q<ObjectField>("Parent-Picker").value;
        String tag = (String)this.rootVisualElement.Q<TagField>("Tag-Field").value;
        int layer = (int)this.rootVisualElement.Q<LayerField>("Layer-Field").value;

        // Position Options
        Vector3 offset = (Vector3)this.rootVisualElement.Q<Vector3Field>("Offset-Field").value;
        bool alignToggle = (bool)this.rootVisualElement.Q<Toggle>("Align-Toggle").value;

        // Rotation Options
        Vector3 rotationMin = (Vector3)this.rootVisualElement.Q<Vector3Field>("RotationMin-Field").value;
        Vector3 rotationMax = (Vector3)this.rootVisualElement.Q<Vector3Field>("RotationMax-Field").value;
        bool normalToggle = (bool)this.rootVisualElement.Q<Toggle>("Normal-Toggle").value;

        // Scale Options
        Vector3 scaleMin = (Vector3)this.rootVisualElement.Q<Vector3Field>("ScaleMin-Field").value;
        Vector3 scaleMax = (Vector3)this.rootVisualElement.Q<Vector3Field>("ScaleMax-Field").value;
        bool scaleUniformToggle = (bool)this.rootVisualElement.Q<Toggle>("ScaleUniform-Toggle").value;

        // Make a ray from GUI point
        Ray guiRay = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
        
        // Translate GUIPointToWorldRay to actual world space or infinity
        RaycastHit hit;
        Vector3 mousePositionInWorld = Physics.Raycast(guiRay, out hit) ? hit.point : Vector3.negativeInfinity;
        if (mousePositionInWorld.x != float.NegativeInfinity)
        {
            // Align to grid of 1 or not
            mousePositionInWorld = alignToggle ? new Vector3(Mathf.Round(mousePositionInWorld.x), Mathf.Round(mousePositionInWorld.y), Mathf.Round(mousePositionInWorld.z)) : mousePositionInWorld;
            
            GameObject spawned;
            
            // Check if the reference is from a prefab, and spawn it as one if it should be
            if (PrefabUtility.IsPartOfAnyPrefab(go) == true)
            {
                go = PrefabUtility.GetCorrespondingObjectFromOriginalSource(go);
                spawned = PrefabUtility.InstantiatePrefab(go) as GameObject;
                spawned.transform.position = mousePositionInWorld + offset;
            }
            else
            {
                // Instantiate with offset and rotation
                spawned = Instantiate(go, mousePositionInWorld + offset, Quaternion.identity);
            }


            // Rotate on normal if toggled on
            if (normalToggle)
            {
                spawned.transform.rotation = Quaternion.FromToRotation(spawned.transform.forward, hit.normal);
                spawned.transform.localRotation *= Quaternion.Euler(90, 0, 0);
            }
            
            // Rotate randomly
            Quaternion rotation = Quaternion.Euler(UnityEngine.Random.Range(rotationMin.x, rotationMax.x), UnityEngine.Random.Range(rotationMin.y, rotationMax.y), UnityEngine.Random.Range(rotationMin.z, rotationMax.z));
            spawned.transform.localRotation *= rotation;

            // Scale randomly
            if (scaleUniformToggle)
            {
                Vector3 randomScale = Vector3.one * UnityEngine.Random.Range(scaleMin.x, scaleMax.x);
                spawned.transform.localScale = randomScale;
            } else
            {
                spawned.transform.localScale = new Vector3(UnityEngine.Random.Range(scaleMin.x, scaleMax.x), UnityEngine.Random.Range(scaleMin.y, scaleMax.y), UnityEngine.Random.Range(scaleMin.z, scaleMax.z));
            }

            // Set the name if the toggle is ticked
            spawned.name = nameToggle ? name : go.name;

            // Set the other properties
            spawned.isStatic = staticToggle ? true : false;

            // Make sure the parent isnt accidentally an asset from folder
            if (parent != null)
            {
                if (!AssetDatabase.Contains(parent))
                {
                    spawned.transform.parent = parent.transform;
                }
                else { Debug.Log("Can't parent the new object to an asset file.");}
            }
            
            spawned.tag = tag;
            spawned.layer = layer;

            // Allow the created object to be undone
            Undo.RegisterCreatedObjectUndo(spawned, "Instantiate Object");
        }
    }
}