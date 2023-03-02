using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using UnityEditor;

public class CalibrateTool : MonoBehaviour
{
    static CalibrateTool instance;
    public static CalibrateTool Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType(typeof(CalibrateTool)) as CalibrateTool;
            }

            return instance;
        }
    }

    /// <summary>
    /// Define the targetParentFolder to save the data;
    /// </summary>
    public string targetParentFolder = @"../calib/";

    /// <summary>
    /// Add the cameras you want to calibrate into this list
    /// </summary>
    public List<Camera> camerasToCalibrate;

    /// <summary>
    /// Fake chessboard will be generated at this transform
    /// </summary>
    public Transform chessboardGenerateCenter;

    /// <summary>
    /// The inteval bettween the chessboard to change its transform
    /// </summary>
    [SerializeField] float updateChessboardInterval = 0.5f;

    /// <summary>
    /// Count how many times(views) is the chessboard going to be updated
    /// </summary>
    [SerializeField] int chessboardCount = 50;

    /// <summary>
    /// The length of the sides of the fake checkerboard
    /// </summary>
    [SerializeField] float SQUARE_SIZE = 0.14f;

    /// <summary>
    /// How many squares in width of the chessboard, better be odd
    /// </summary>
    [SerializeField] int widthOfChessboard = 9;

    /// <summary>
    /// How many squares in height of the chessboard, better be odd
    /// </summary>
    [SerializeField] int heightOfChessboard = 7;

    /// <summary>
    /// Random offsets for updating the chessboard 
    /// </summary>
    [SerializeField] float tRandomOffset = 5f;
    [SerializeField] float rRandomOffset = 120;


    /// <summary>
    /// A dictionary to store the points(GameObject) and their coordinates(follow OpenCV,(x,y,0)) of the chessboard
    /// </summary>
    Dictionary<GameObject, Vector3> markPointsDic = new Dictionary<GameObject, Vector3>();

    /// <summary>
    /// Index of chessboard
    /// </summary>
    int boardIndex = 0;

    private void Awake()
    {
        GenerateMarkPoints();
    }

    void Start()
    {
        targetParentFolder = targetParentFolder + "\\calib";
        Debug.Log("[IO][Calib]Calibrate saved in " + targetParentFolder);
        ResetFolder(targetParentFolder);
        //BeginToCalibrate();
    }

    void BeginToCalibrate() 
    {
        if (camerasToCalibrate.Count < 1)
        {
            Debug.LogWarning("[Calib] No camera to calibrate, please check " + nameof(CalibrateTool) + "." + nameof(camerasToCalibrate));
            return;
        }
        StartCoroutine(Func());
    }



    IEnumerator Func()
    {
        while (true)// or for(i;i;i)
        {
            yield return new WaitForSeconds(updateChessboardInterval); // first
            //Specific functions put here 

            int cameraIndex = 0;
            foreach (var cam in camerasToCalibrate)
            {
                WriteChessboardScreenPos(cam, cameraIndex);
                cameraIndex++;
            }
            UpdateChessboard();
            // Note the order of codes above.  Different order shows different outcome.
            if (boardIndex >= chessboardCount) 
            {
                break;
            }
        }
    }



    /// <summary>
    /// Generate mark points around the chessboardGenerateCenter
    /// </summary>
    void GenerateMarkPoints()
    {
        for (int w = 0; w < widthOfChessboard; w++)
        {
            for (int h =0; h < heightOfChessboard; h++)
            {
                GameObject markPoint = new GameObject(w.ToString() + "_" + h.ToString());
                markPoint.transform.position = new Vector3(w * SQUARE_SIZE, 0, h * SQUARE_SIZE);
                markPoint.transform.parent = transform;
                markPointsDic.Add(markPoint, new Vector3(w * SQUARE_SIZE, h * SQUARE_SIZE, 0));
            }
        }
        gameObject.transform.position = chessboardGenerateCenter.transform.position;
        gameObject.transform.rotation = chessboardGenerateCenter.transform.rotation;
        //gameObject.transform.rotation = Quaternion.identity;
        //foreach (var go in markPointsDic)
        //{
        //Debug.Log(go.Key.transform.position);
        //}
    }



    void UpdateChessboard() 
    {
        boardIndex++;
        System.Random ran = new System.Random();
        transform.position = new Vector3(NextFloat(ran, tRandomOffset, -tRandomOffset), NextFloat(ran, tRandomOffset, -tRandomOffset), NextFloat(ran, tRandomOffset, -tRandomOffset));
        transform.rotation = Quaternion.Euler(NextFloat(ran, rRandomOffset, -rRandomOffset), NextFloat(ran, rRandomOffset, -rRandomOffset), NextFloat(ran, rRandomOffset, -rRandomOffset));
    }

    void WriteChessboardScreenPos(Camera cam, int cameraIndex)
    {
        List<Vector3> validObjectPointsList = new List<Vector3>();
        List<Vector3> outPutObjList = new List<Vector3>();

        foreach (var go in markPointsDic)
        {
            Vector3 viewPos = cam.WorldToViewportPoint(go.Key.transform.position);
            if (viewPos.x >= 0 && viewPos.x <= 1 && viewPos.y >= 0 && viewPos.y <= 1 && viewPos.z > 0)
            {
                validObjectPointsList.Add(go.Key.transform.position);
                outPutObjList.Add(go.Value);
            }
        }



        Vector3[] validObjectPoints = validObjectPointsList.ToArray();
        Vector2[] imagePoints = ConvertWorldPointsToScreenPointsOpenCV(validObjectPoints, cam);

        if (boardIndex == 0)
        {
            foreach (var point in validObjectPoints)
            {
                Debug.Log("[Calib]The first view is: ");
                Debug.Log(point);
            }
        }

        string file_name = boardIndex + ".txt";
        string file_name_3d =  boardIndex + "_3d.txt";

        StreamWriter sw = CreateSW(cameraIndex, file_name);
        StreamWriter sw_3d = CreateSW(cameraIndex, file_name_3d);
        WriteArrayToFile(imagePoints, ref sw);
        WriteArrayToFile(outPutObjList.ToArray(), ref sw_3d);

        sw.Close();
        sw_3d.Close();
    }




    public void ResetFolder(string foldername)
    {
        DirectoryInfo dir = new DirectoryInfo(foldername);

        if (dir.Exists)
        {
            dir.Delete(true);
            Debug.Log("[IO]" + foldername + " have been reset");
        }

    }

    StreamWriter CreateSW(int cameraIdx, string filename)
    {
        StreamWriter sw;
        cameraIdx++;

        Directory.CreateDirectory(targetParentFolder + "/C" + cameraIdx.ToString() + "/");
        FileInfo fileInfo = new FileInfo(targetParentFolder + "/C" + cameraIdx.ToString() + "/" + filename);
        if (!fileInfo.Exists)
        {
            sw = fileInfo.CreateText();//����һ������д�� UTF-8 ������ı�  
            Debug.Log("[IO]File " + filename + " has been inited");

            return sw;
        }
        else
        {
            sw = fileInfo.AppendText();//������ UTF-8 �����ı��ļ��Խ��ж�ȡ  
            return sw;
        }

        Debug.Log(fileInfo);
    }

    public void WriteArrayToFile(Vector2[] array, ref StreamWriter sw)
    {
        // Create a new StreamWriter to write the array to a text file

        // Loop through each element in the array and write it to the file
        foreach (Vector3 element in array)
        {
            sw.WriteLine(element.x + " " + element.y);
        }
        //Debug.Log("[IO]Vector3 array written to file: " + filename + ".txt");
    }

    public void WriteArrayToFile(Vector3[] array, ref StreamWriter sw)
    {
        // Create a new StreamWriter to write the array to a text file

        // Loop through each element in the array and write it to the file
        foreach (Vector3 element in array)
        {
            sw.WriteLine(element.x + " " + element.y + " " + element.z);
        }
        //Debug.Log("[IO]Vector3 array written to file: " + filename + ".txt");
    }

    public float NextFloat(System.Random ran, float minValue, float maxValue)
    {
        return (float)(ran.NextDouble() * (maxValue - minValue) + minValue);
    }

    public Vector2[] ConvertWorldPointsToScreenPointsOpenCV(Vector3[] worldPoints, Camera cam)
    {
        var points2D = new Vector2[worldPoints.Length];
        for (int i = 0; i < worldPoints.Length; i++)
        {

            points2D[i] = cam.WorldToScreenPoint(worldPoints[i]);
            points2D[i].y = cam.pixelHeight - points2D[i].y;
            //Debug.Log(points2D[i]);
            //Debug.Log(points2D[i]);
        }
        return points2D;

    }

    [ExecuteInEditMode]
    private void OnDrawGizmos()
    {
        foreach (var go in markPointsDic)
        {
            Gizmos.color = new Color(1, 0, 0, 0.5f);
            Gizmos.DrawCube(go.Key.transform.position, new Vector3(0.01f, 0.01f, 0.01f));
            Handles.Label(go.Key.transform.position, go.Value.ToString());
        }
    }
}