#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// One-click builder for Puzzle 2 (Starting Lineup).
/// Menu: Tools > Volley Vision > Build Puzzle 2
/// Creates the jersey prefab, the lineup board with 6 sockets, the status light/text,
/// six jerseys + the clue clipboard on the bench, and saves it all as Puzzle2.prefab under "Puzzles".
/// Requires an Interaction Layer named "Jersey" to exist first.
/// </summary>
public static class Puzzle2Builder
{
    const string PrefabDir = "Assets/Prefabs/Puzzle2";
    const string MaterialDir = "Assets/Materials/Puzzle2";

    // Board on the west wall, facing the gym (+X). Board "front" is its local -Z.
    static readonly Vector3 BoardPos = new Vector3(-6.8f, 1.6f, -5f);
    static readonly Quaternion BoardRot = Quaternion.Euler(0f, -90f, 0f);

    // Bench (from Gym.unity): centered x=-5.55, z=0, top surface at y=1.0, long axis along Z.
    const float BenchX = -5.55f;
    const float BenchTopY = 1.0f;

    static readonly int[] JerseyNumbers = { 3, 7, 9, 12, 15, 21 };

    // zone, expected jersey, board-local center
    static readonly (int zone, int jersey, Vector2 pos)[] Zones =
    {
        (4, 12, new Vector2(-0.6f,  0.17f)), (3, 21, new Vector2(0f,  0.17f)), (2, 3,  new Vector2(0.6f,  0.17f)),
        (5, 9,  new Vector2(-0.6f, -0.37f)), (6, 15, new Vector2(0f, -0.37f)), (1, 7,  new Vector2(0.6f, -0.37f)),
    };

    const string ClueText =
        "<b>STARTING LINEUP</b>\n\n" +
        "1. (rule 1 here)\n\n" +
        "2. (rule 2 here)\n\n" +
        "3. (rule 3 here)\n\n" +
        "4. (rule 4 here)";

    [MenuItem("Tools/Volley Vision/Build Puzzle 2")]
    public static void Build()
    {
        int jerseyLayer = InteractionLayerMask.GetMask("Jersey");
        if (jerseyLayer == 0)
        {
            EditorUtility.DisplayDialog("Puzzle 2",
                "No Interaction Layer named \"Jersey\" yet.\n\nProject Settings > XR Plug-in Management > XR Interaction Toolkit > Interaction Layer Settings, name an empty slot \"Jersey\", then run this again.",
                "OK");
            return;
        }
        if (GameObject.Find("Puzzle2") != null)
        {
            EditorUtility.DisplayDialog("Puzzle 2", "A \"Puzzle2\" object is already in the scene. Delete it first if you want to rebuild.", "OK");
            return;
        }

        EnsureFolder(PrefabDir);
        EnsureFolder(MaterialDir);

        var whiteMat  = MakeMaterial("Whiteboard", new Color(0.95f, 0.95f, 0.95f));
        var zoneMat   = MakeMaterial("ZoneSquare", new Color(0.82f, 0.86f, 0.92f));
        var lineMat   = MakeMaterial("CourtLine",  new Color(0.15f, 0.15f, 0.2f));
        var jerseyMat = MakeMaterial("JerseyTeam", new Color(0.29f, 0.61f, 0.83f)); // Carolina blue
        var lightMat  = MakeMaterial("StatusLight", new Color(1f, 0.8f, 0.3f), emissive: true);
        var clipMat   = MakeMaterial("ClipboardBack", new Color(0.45f, 0.3f, 0.18f));

        // ---------- Jersey prefab ----------
        var jerseyPrefab = BuildJerseyPrefab(jerseyMat, jerseyLayer);

        // ---------- Root ----------
        var root = new GameObject("Puzzle2");
        var puzzles = GameObject.Find("Puzzles");
        if (puzzles != null) root.transform.SetParent(puzzles.transform, false);

        // ---------- Board ----------
        var board = new GameObject("Puzzle2_Lineup");
        board.transform.SetParent(root.transform, false);
        board.transform.SetPositionAndRotation(BoardPos, BoardRot);

        Box("Whiteboard", board.transform, Vector3.zero, new Vector3(2.0f, 1.5f, 0.05f), whiteMat);

        const float face = -0.027f;
        // Net + court outline
        Box("Net", board.transform, new Vector3(0f, 0.46f, face), new Vector3(1.85f, 0.03f, 0.004f), lineMat);
        Box("Line_Left",   board.transform, new Vector3(-0.92f, -0.1f, face), new Vector3(0.012f, 1.12f, 0.004f), lineMat);
        Box("Line_Right",  board.transform, new Vector3( 0.92f, -0.1f, face), new Vector3(0.012f, 1.12f, 0.004f), lineMat);
        Box("Line_Bottom", board.transform, new Vector3(0f, -0.66f, face), new Vector3(1.85f, 0.012f, 0.004f), lineMat);
        Box("Line_Attack", board.transform, new Vector3(0f, -0.10f, face), new Vector3(1.85f, 0.008f, 0.004f), lineMat);
        Text("NetLabel", board.transform, "NET", new Vector3(0f, 0.52f, -0.03f), new Vector2(0.4f, 0.07f), Color.black, 1.5f);
        Text("RotationLabel", board.transform, "ROTATION: CLOCKWISE  (2 > 1 > 6 > 5 > 4 > 3)",
            new Vector3(0f, -0.70f, -0.03f), new Vector2(1.6f, 0.06f), new Color(0.2f, 0.2f, 0.25f), 1f);

        // Status light strip + status text
        var strip = Box("StatusLight", board.transform, new Vector3(0f, 0.68f, -0.03f), new Vector3(1.9f, 0.04f, 0.02f), lightMat);
        var status = Text("StatusText", board.transform, "", new Vector3(0f, 0.95f, -0.03f), new Vector2(2f, 0.25f), new Color(1f, 0.25f, 0.2f), 3f);
        status.fontStyle = FontStyles.Bold;

        // Zones + sockets
        foreach (var z in Zones)
        {
            var c = new Vector3(z.pos.x, z.pos.y, face);
            Box($"ZoneSquare{z.zone}", board.transform, c, new Vector3(0.5f, 0.5f, 0.004f), zoneMat);
            Text($"ZoneLabel{z.zone}", board.transform, z.zone.ToString(),
                new Vector3(z.pos.x - 0.19f, z.pos.y + 0.19f, -0.03f), new Vector2(0.08f, 0.08f), new Color(0.3f, 0.35f, 0.45f), 1.5f);

            var slot = new GameObject($"Zone{z.zone}_Socket");
            slot.transform.SetParent(board.transform, false);
            slot.transform.localPosition = new Vector3(z.pos.x, z.pos.y, -0.025f);

            var col = slot.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0.12f;

            var attach = new GameObject("Attach").transform;
            attach.SetParent(slot.transform, false);
            attach.localPosition = new Vector3(0f, 0f, -0.03f); // 3 cm off the board, toward the gym

            var socket = slot.AddComponent<XRSocketInteractor>();
            socket.interactionLayers = jerseyLayer;
            socket.showInteractableHoverMeshes = true;
            socket.attachTransform = attach;

            var ls = slot.AddComponent<LineupSlot>();
            var so = new SerializedObject(ls);
            so.FindProperty("zone").intValue = z.zone;
            so.FindProperty("expectedJersey").intValue = z.jersey;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // LineupBoard + audio
        var audio = board.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 1f;
        var lb = board.AddComponent<LineupBoard>();
        var bso = new SerializedObject(lb);
        bso.FindProperty("statusLight").objectReferenceValue = strip.GetComponent<Renderer>();
        bso.FindProperty("statusText").objectReferenceValue = status;
        bso.FindProperty("audioSource").objectReferenceValue = audio;
        var pop = FindClip("Button Pop");
        if (pop != null)
        {
            bso.FindProperty("mistakeClip").objectReferenceValue = pop;
            bso.FindProperty("solvedClip").objectReferenceValue = pop;
        }
        bso.ApplyModifiedPropertiesWithoutUndo();

        // ---------- Props on the bench ----------
        var props = new GameObject("Puzzle2_Props");
        props.transform.SetParent(root.transform, false);
        var lieFlat = Quaternion.Euler(90f, 0f, 0f); // front (number) faces up

        for (int i = 0; i < JerseyNumbers.Length; i++)
        {
            var j = (GameObject)PrefabUtility.InstantiatePrefab(jerseyPrefab, props.transform);
            j.name = $"Jersey_{JerseyNumbers[i]}";
            j.transform.SetPositionAndRotation(new Vector3(BenchX, BenchTopY + 0.05f, -0.78f + i * 0.24f), lieFlat);

            var jc = j.GetComponent<Jersey>();
            var jso = new SerializedObject(jc);
            jso.FindProperty("number").intValue = JerseyNumbers[i];
            jso.ApplyModifiedPropertiesWithoutUndo();
            j.GetComponentInChildren<TextMeshPro>().text = JerseyNumbers[i].ToString();
        }

        BuildClipboard(props.transform, clipMat, whiteMat, new Vector3(BenchX, BenchTopY + 0.05f, 0.72f), lieFlat);

        // ---------- Save as a prefab so the scene change is one prefab instance ----------
        PrefabUtility.SaveAsPrefabAssetAndConnect(root, $"{PrefabDir}/Puzzle2.prefab", InteractionMode.AutomatedAction);
        Undo.RegisterCreatedObjectUndo(root, "Build Puzzle 2");
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;

        Debug.Log(pop == null
            ? "[Puzzle2Builder] Built Puzzle 2. No placeholder sound found; assign Mistake/Solved clips on Puzzle2_Lineup."
            : "[Puzzle2Builder] Built Puzzle 2. Placeholder sound is 'Button Pop'; swap in real clips on Puzzle2_Lineup.");
    }

    static GameObject BuildJerseyPrefab(Material mat, int jerseyLayer)
    {
        var path = $"{PrefabDir}/Jersey.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;

        var go = new GameObject("Jersey");
        var box = go.AddComponent<BoxCollider>();
        box.size = new Vector3(0.18f, 0.22f, 0.03f);

        Box("Body", go.transform, Vector3.zero, new Vector3(0.18f, 0.22f, 0.03f), mat);
        var label = Text("Number", go.transform, "0", new Vector3(0f, 0f, -0.016f), new Vector2(0.16f, 0.16f), Color.white, 2f);
        label.fontStyle = FontStyles.Bold;

        var rb = go.AddComponent<Rigidbody>();
        rb.mass = 0.2f;
        rb.useGravity = true;

        var grab = go.AddComponent<XRGrabInteractable>();
        grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
        grab.throwOnDetach = false;
        grab.interactionLayers = InteractionLayerMask.GetMask("Default") | jerseyLayer;

        var jersey = go.AddComponent<Jersey>();
        var so = new SerializedObject(jersey);
        var labels = so.FindProperty("numberLabels");
        labels.arraySize = 1;
        labels.GetArrayElementAtIndex(0).objectReferenceValue = label;
        so.ApplyModifiedPropertiesWithoutUndo();

        var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        return prefab;
    }

    static void BuildClipboard(Transform parent, Material backMat, Material paperMat, Vector3 pos, Quaternion rot)
    {
        var go = new GameObject("Clue_Clipboard");
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(pos, rot);

        var box = go.AddComponent<BoxCollider>();
        box.size = new Vector3(0.23f, 0.32f, 0.01f);
        Box("Back", go.transform, Vector3.zero, new Vector3(0.23f, 0.32f, 0.01f), backMat);
        Box("Paper", go.transform, new Vector3(0f, -0.01f, -0.006f), new Vector3(0.21f, 0.28f, 0.002f), paperMat);

        var t = Text("LineupCard", go.transform, ClueText, new Vector3(0f, -0.01f, -0.0075f), new Vector2(0.19f, 0.26f), Color.black, 0.25f);
        t.alignment = TextAlignmentOptions.TopLeft;

        var rb = go.AddComponent<Rigidbody>();
        rb.mass = 0.3f;
        var grab = go.AddComponent<XRGrabInteractable>();
        grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
        grab.throwOnDetach = false;
    }

    // ---------- helpers ----------
    static GameObject Box(string name, Transform parent, Vector3 localPos, Vector3 size, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>()); // visuals only
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    static TextMeshPro Text(string name, Transform parent, string text, Vector3 localPos, Vector2 size, Color color, float maxFont)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.rectTransform.sizeDelta = size;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 0.02f;
        tmp.fontSizeMax = maxFont;
        return tmp;
    }

    static Material MakeMaterial(string name, Color color, bool emissive = false)
    {
        var path = $"{MaterialDir}/{name}.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var mat = new Material(shader);
        mat.SetColor("_BaseColor", color);
        mat.color = color;
        if (emissive)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", color * 3f);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    static AudioClip FindClip(string clipName)
    {
        foreach (var guid in AssetDatabase.FindAssets($"{clipName} t:AudioClip"))
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid));
            if (clip != null) return clip;
        }
        return null;
    }

    static void EnsureFolder(string path)
    {
        var parts = path.Split('/');
        var current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            var next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
#endif
