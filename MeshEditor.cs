using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// This class is used to create a 2D Mesh Asset for Pathfinding
/// </summary>
public class MeshEditor : EditorWindow
{
    [Header("Mesh Data")]
    private List<Vector2> vertices = new List<Vector2>();
    private List<Triangle> triangles = new List<Triangle>();

    [Header("Editor State Handling")]
    private bool isCreatingMesh = false;
    private bool isCurrentMeshFinished = false;
    private bool isSceneCleared = false;
    private bool isDragging = false;
    private int draggedVertexIndex = -1;

    [MenuItem("Window/2D Mesh Editor")]
    public static void ShowWindow()
    {
        GetWindow<MeshEditor>("2D Mesh Editor");
    }

    #region GUI Functions
    private void OnGUI()
    {
        EditorGUILayout.HelpBox("Guidelines:\n" +
                         "- Click left mouse button to place a vertex (vertices will connect automatically)\n" +
                         "- Hold left mouse button to drag vertex around\n" +
                         "- Press backspace to remove last placed vertex",
                         MessageType.Info);

        EditorGUI.BeginDisabledGroup(isCreatingMesh || isCurrentMeshFinished);
        if (GUILayout.Button("Start Creating Mesh"))
        {
            isCreatingMesh = true;
            isCurrentMeshFinished = false;
            isSceneCleared = false;
            vertices.Clear();
            triangles.Clear();

            SceneView.duringSceneGui += OnSceneGUI;
        }
        EditorGUI.EndDisabledGroup();

        if (isCreatingMesh)
        {
            GUILayout.Label("Click in the Scene View to add vertices", EditorStyles.wordWrappedLabel);
        }

        EditorGUI.BeginDisabledGroup(!isCreatingMesh);
        if (GUILayout.Button("Finish Creating Mesh"))
        {
            isCreatingMesh = false;
            isCurrentMeshFinished = true;
            Debug.Log(isSceneCleared);

            SceneView.duringSceneGui -= OnSceneGUI;
        }

        if (GUILayout.Button("Clear Mesh"))
        {
            isSceneCleared = true;
            vertices.Clear();
            triangles.Clear();
            isCurrentMeshFinished = false;
            SceneView.RepaintAll();
        }
        EditorGUI.EndDisabledGroup();

        EditorGUI.BeginDisabledGroup(!isCurrentMeshFinished);
        if (GUILayout.Button("Edit"))
        {
            isCreatingMesh = true;
            isCurrentMeshFinished = false;

            SceneView.duringSceneGui += OnSceneGUI;
        }

        if (GUILayout.Button("Save Mesh"))
        {
            SaveMesh();
        }
        EditorGUI.EndDisabledGroup();

        if (isCreatingMesh)
        {
            EditorGUILayout.HelpBox("For optimal use, do not place vertices inside already existing triangles", MessageType.Warning);
        }
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        Event e = Event.current;
        Vector2 mousePosition = HandleUtility.GUIPointToWorldRay(e.mousePosition).origin;

        if (!isCreatingMesh && !isSceneCleared)
        {
            DrawVertices(vertices);
            DrawTriangleLines(vertices, triangles);
            return;
        }

        if (!isCreatingMesh)
            return;

        if (e.type == EventType.MouseDown && e.button == 0)
        {
            int vertexIndex = GetVertexUnderMouse(mousePosition);
            if (vertexIndex != -1)
            {
                isDragging = true;
                draggedVertexIndex = vertexIndex;
                e.Use();
                return;
            }

            vertices.Add(mousePosition);
            isSceneCleared = false;
            if (vertices.Count > 2)
            {
                int[] nearestVertexIndices = FindNearestVertices(vertices, mousePosition);

                if (!AreVerticesPartOfSameTriangle(vertices[nearestVertexIndices[0]], vertices[nearestVertexIndices[1]]))
                {
                    List<int> trianglesWithNearestIndex1 = FindAllTriangleIndicesFromVector(vertices[nearestVertexIndices[0]]);
                    List<int> trianglesWithNearestIndex2 = FindAllTriangleIndicesFromVector(vertices[nearestVertexIndices[1]]);
                    Debug.Log(FindJoinedVertexIndexBetweenTriangles(trianglesWithNearestIndex1, trianglesWithNearestIndex2));

                    int sharedVertexIndex = FindJoinedVertexIndexBetweenTriangles(trianglesWithNearestIndex1, trianglesWithNearestIndex2);

                    if (sharedVertexIndex != -1)
                    {
                        Triangle newTriangle1 = new Triangle(nearestVertexIndices[0], sharedVertexIndex, vertices.Count - 1);
                        Triangle newTriangle2 = new Triangle(nearestVertexIndices[1], sharedVertexIndex, vertices.Count - 1);
                        triangles.Add(newTriangle1);
                        triangles.Add(newTriangle2);

                        return;
                    }
                }

                Triangle newTriangle = new Triangle(nearestVertexIndices[0], nearestVertexIndices[1], vertices.Count - 1);
                triangles.Add(newTriangle);
            }
            e.Use();
            SceneView.RepaintAll();
        }

        if (e.type == EventType.MouseDrag && isDragging)
        {
            if (draggedVertexIndex != -1)
            {
                vertices[draggedVertexIndex] = mousePosition;
                SceneView.RepaintAll();
            }
            e.Use();
        }

        if (e.type == EventType.MouseUp && e.button == 0)
        {
            if (isDragging)
            {
                isDragging = false;
                draggedVertexIndex = -1;
                e.Use();
            }
        }

        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Backspace)
        {
            if (vertices.Count < 1) return;
            RemoveLastVertexAndAssociatedTriangles();
            e.Use();
        }

        DrawLinesTo2NearestVertices(mousePosition);
        DrawVertices(vertices);
        DrawTriangleLines(vertices, triangles);
    }
    #endregion

    #region Saving Mesh
    [ContextMenu("Save Mesh")]
    private void SaveMesh()
    {
        if (vertices.Count < 3 || triangles.Count < 1)
        {
            Debug.LogWarning("Not enough vertices or triangles to save a mesh.");
            return;
        }

        Vector3[] meshVertices = new Vector3[vertices.Count];
        for (int i = 0; i < vertices.Count; i++)
        {
            meshVertices[i] = new Vector3(vertices[i].x, vertices[i].y, 0);
        }

        int[] meshTriangles = new int[triangles.Count * 3];
        for (int i = 0; i < triangles.Count; i++)
        {
            meshTriangles[i * 3] = triangles[i].m_Vertex1;
            meshTriangles[i * 3 + 1] = triangles[i].m_Vertex2;
            meshTriangles[i * 3 + 2] = triangles[i].m_Vertex3;
        }

        string path = EditorUtility.SaveFilePanelInProject("Save Mesh", "NewMesh", "asset", "Please enter a file name to save the mesh");
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogWarning("Save operation cancelled.");
            return;
        }

        Mesh mesh = new Mesh();
        mesh.vertices = meshVertices;
        mesh.triangles = meshTriangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        AssetDatabase.CreateAsset(mesh, path);
        AssetDatabase.SaveAssets();

        Debug.Log("Mesh saved at " + path);

        ResetUI();
    }
    #endregion

    #region Helper Functions
    private void ResetUI()
    {
        isCreatingMesh = false;
        isCurrentMeshFinished = false;
        isSceneCleared = true;
        vertices.Clear();
        triangles.Clear();
        SceneView.RepaintAll();
    }

    private int GetVertexUnderMouse(Vector2 mousePosition)
    {
        for (int i = 0; i < vertices.Count; i++)
        {
            if (Vector2.Distance(vertices[i], mousePosition) < 0.3f) 
            {
                return i;
            }
        }
        return -1;
    }

    private bool AreVerticesPartOfSameTriangle(Vector2 vector1, Vector2 vector2)
    {
        foreach (var triangle in triangles)
        {
            Vector2 firstTriangleVertex = vertices[triangle.m_Vertex1];
            Vector2 secondTriangleVertex = vertices[triangle.m_Vertex2];
            Vector2 thirdTriangleVertex = vertices[triangle.m_Vertex3];

            bool isFirstVertexInTriangle = (vector1 == firstTriangleVertex || vector1 == secondTriangleVertex || vector1 == thirdTriangleVertex);
            bool isSecondVertexInTriangle = (vector2 == firstTriangleVertex || vector2 == secondTriangleVertex || vector2 == thirdTriangleVertex);

            if (isFirstVertexInTriangle && isSecondVertexInTriangle)
            {
                return true;
            }
        }

        return false;
    }

    private List<int> FindAllTriangleIndicesFromVector(Vector2 _vector)
    {
        List<int> output = new List<int>();

        for (int i = 0; i < triangles.Count; i++)
        {
            if (vertices[triangles[i].m_Vertex1] == _vector || vertices[triangles[i].m_Vertex2] == _vector || vertices[triangles[i].m_Vertex3] == _vector)
            {
                output.Add(i);
            }
        }
        return output;
    }

    private int FindJoinedVertexIndexBetweenTriangles(List<int> _trianglesAroundFirstPoint, List<int> _trianglesAroundSecondPoint)
    {
        for (int i = 0; i < _trianglesAroundFirstPoint.Count; i++)
        {
            int firstPointTriangleFirstVertex = triangles[_trianglesAroundFirstPoint[i]].m_Vertex1;
            int firstPointTriangleSecondVertex = triangles[_trianglesAroundFirstPoint[i]].m_Vertex2;
            int firstPointTriangleThirdVertex = triangles[_trianglesAroundFirstPoint[i]].m_Vertex3;

            for (int j = 0; j < _trianglesAroundSecondPoint.Count; j++)
            {
                int secondPointTriangleFirstVertex = triangles[_trianglesAroundSecondPoint[j]].m_Vertex1;
                int secondPointTriangleSecondVertex = triangles[_trianglesAroundSecondPoint[j]].m_Vertex2;
                int secondPointTriangleThirdVertex = triangles[_trianglesAroundSecondPoint[j]].m_Vertex3;


                if (firstPointTriangleFirstVertex == secondPointTriangleFirstVertex ||
                firstPointTriangleFirstVertex == secondPointTriangleSecondVertex ||
                firstPointTriangleFirstVertex == secondPointTriangleThirdVertex)
                {
                    return firstPointTriangleFirstVertex;
                }
                if (firstPointTriangleSecondVertex == secondPointTriangleFirstVertex ||
                    firstPointTriangleSecondVertex == secondPointTriangleSecondVertex ||
                    firstPointTriangleSecondVertex == secondPointTriangleThirdVertex)
                {
                    return firstPointTriangleSecondVertex;
                }
                if (firstPointTriangleThirdVertex == secondPointTriangleFirstVertex ||
                    firstPointTriangleThirdVertex == secondPointTriangleSecondVertex ||
                    firstPointTriangleThirdVertex == secondPointTriangleThirdVertex)
                {
                    return firstPointTriangleThirdVertex;
                }
            }
        }
        return -1;
    }

    private int[] FindNearestVertices(List<Vector2> vertices, Vector2 mousePosition)
    {
        int[] nearestIndices = new int[2];
        float[] nearestDistances = { float.MaxValue, float.MaxValue };

        for (int i = 0; i < vertices.Count - 1; i++)
        {
            float distance = Vector2.Distance(vertices[i], mousePosition);
            if (distance < nearestDistances[0])
            {
                nearestDistances[1] = nearestDistances[0];
                nearestIndices[1] = nearestIndices[0];
                nearestDistances[0] = distance;
                nearestIndices[0] = i;
            }
            else if (distance < nearestDistances[1])
            {
                nearestDistances[1] = distance;
                nearestIndices[1] = i;
            }
        }
        return nearestIndices;
    }

    private int[] FindNearestVerticesForUI(List<Vector2> vertices, Vector2 mousePosition)
    {
        int[] nearestIndices = new int[2];
        float[] nearestDistances = { float.MaxValue, float.MaxValue };

        for (int i = 0; i < vertices.Count; i++)
        {
            float distance = Vector2.Distance(vertices[i], mousePosition);
            if (distance < nearestDistances[0])
            {
                nearestDistances[1] = nearestDistances[0];
                nearestIndices[1] = nearestIndices[0];
                nearestDistances[0] = distance;
                nearestIndices[0] = i;
            }
            else if (distance < nearestDistances[1])
            {
                nearestDistances[1] = distance;
                nearestIndices[1] = i;
            }
        }
        return nearestIndices;
    }

    private void RemoveLastVertexAndAssociatedTriangles()
    {
        if (vertices.Count <= 0)
            return;

        Vector2 lastVertex = vertices[vertices.Count - 1];
        List<int> trianglesToRemove = new List<int>();

        for (int i = 0; i < triangles.Count; i++)
        {
            if (vertices[triangles[i].m_Vertex1] == lastVertex ||
                vertices[triangles[i].m_Vertex2] == lastVertex ||
                vertices[triangles[i].m_Vertex3] == lastVertex)
            {
                trianglesToRemove.Add(i);
            }
        }

        trianglesToRemove.Sort();
        trianglesToRemove.Reverse();

        foreach (int index in trianglesToRemove)
        {
            triangles.RemoveAt(index);
        }

        vertices.RemoveAt(vertices.Count - 1);
    }

    private void DrawLinesTo2NearestVertices(Vector2 mousePosition)
    {
        Handles.color = Color.blue;

        if (vertices.Count == 1)
        {
            Handles.DrawLine(vertices[0], mousePosition);
            SceneView.RepaintAll();
        }

        if (vertices.Count >= 2)
        {
            int[] nearestIndices = FindNearestVerticesForUI(vertices, mousePosition);
            Handles.DrawLine(mousePosition, vertices[nearestIndices[0]]);
            Handles.DrawLine(mousePosition, vertices[nearestIndices[1]]);
            SceneView.RepaintAll();
        }
    }

    private void DrawTriangleLines(List<Vector2> vertices, List<Triangle> triangles)
    {
        Handles.color = Color.magenta;

        if (vertices.Count == 2)
        {
            Handles.DrawLine(vertices[0], vertices[1]);
        }

        for (int i = 0; i < triangles.Count; i++)
        {
            Handles.DrawLine(vertices[triangles[i].m_Vertex1], vertices[triangles[i].m_Vertex2]);
            Handles.DrawLine(vertices[triangles[i].m_Vertex2], vertices[triangles[i].m_Vertex3]);
            Handles.DrawLine(vertices[triangles[i].m_Vertex3], vertices[triangles[i].m_Vertex1]);
        }
    }

    private void DrawVertices(List<Vector2> vertices)
    {
        Handles.color = Color.green;
        foreach (var vertex in vertices)
        {
            Handles.DrawSolidDisc(vertex, Vector3.forward, 0.1f);
        }
    }
    #endregion

    #region Unity Functions

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        vertices.Clear();
        triangles.Clear();
        SceneView.RepaintAll();
        isCreatingMesh = false;
        isCurrentMeshFinished = false;
    }
    #endregion
}
