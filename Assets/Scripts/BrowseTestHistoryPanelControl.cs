using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using UnityEngine;
using UnityEngine.UI;

public class BrowseTestHistoryPanelControl : MonoBehaviour
{
    private Main main;

    private Patient lastPatient;

    private Text patientNameLabel;
    private Text patientAgeLabel;
    private Text eyeLabel;
    private Text testDurationLabel;
    private Text testDateTimeLabel;
    private Image testResultsImage;

    private List<GameObject> testHistoryList;
    private TestInfo currentlySelectedTest;
    public GameObject listItemTemplate;

    void Awake()
    {
        Debug.Log("BrowseTestHistoryPanelControl:Awake()!");

        lastPatient = null;

        // get a reference to the Main script
        main = GameObject.Find("AppControl").GetComponent<Main>();
        patientNameLabel = GameObject.Find("/Canvas/BrowseTestHistoryPanel/PatientNameLabel").GetComponent<Text>();
        patientAgeLabel = GameObject.Find("/Canvas/BrowseTestHistoryPanel/PatientAgeLabel").GetComponent<Text>();
        eyeLabel = GameObject.Find("/Canvas/BrowseTestHistoryPanel/EyeLabel").GetComponent<Text>();
        testDurationLabel = GameObject.Find("/Canvas/BrowseTestHistoryPanel/TestDurationLabel").GetComponent<Text>();
        testDateTimeLabel = GameObject.Find("/Canvas/BrowseTestHistoryPanel/TestDateTimeLabel").GetComponent<Text>();
        testResultsImage = GameObject.Find("/Canvas/BrowseTestHistoryPanel/TestResultsImage").GetComponent<Image>();

        testHistoryList = new List<GameObject>();
        currentlySelectedTest = null;
    }

    /*
    public void addTestToHistory(TestInfo ti)
    {
        if (ti != null)
        {
            GameObject newListItem = Instantiate(listItemTemplate) as GameObject;
            newListItem.SetActive(true);
            string text = ti.dateTime.ToString("yyyy-MMM-dd HH:mm:ss") + ", " + (ti.type == TestType.LeftEye ? "left" : "right");
            newListItem.GetComponent<TestHistoryListItem>().setData(text, ti);
            newListItem.transform.SetParent(listItemTemplate.transform.parent, false);

            testHistoryList.Add(newListItem);
        }
        else
            Debug.Log("Tried adding to test history with null test info!");
    }*/

    private void OnEnable()
    {
        Debug.Log("BrowseTestHistoryPanelControl:OnEnable()!");

        if (main.currentPatient == null)
            Debug.Log("BrowseTestHistoryPanel accessed with null patient!");
        else
        {
            patientNameLabel.text = "Patient Name: " + main.currentPatient.name;
            patientAgeLabel.text = "Patient Age: " + main.currentPatient.age;

            if (main.currentPatient != lastPatient)
            {
                Debug.Log("populating test history...");
                destroyOldTestHistoryList();
                lastPatient = main.currentPatient;
                populateTestHistoryList();
                Debug.Log("done");
            }
        }
    }

    private void destroyOldTestHistoryList()
    {
        foreach (GameObject o in testHistoryList)
            Destroy(o);
        testHistoryList.Clear();
    }

    private void populateTestHistoryList()
    {
        foreach (TestInfo ti in lastPatient.testHistory)
        {
            GameObject newListItem = Instantiate(listItemTemplate) as GameObject;
            newListItem.SetActive(true);
            string text = ti.dateTime.ToString("yyyy-MMM-dd HH:mm:ss") + ", " + (ti.type == TestType.LeftEye ? "left" : "right");
            newListItem.GetComponent<TestHistoryListItem>().setData(text, ti);
            newListItem.transform.SetParent(listItemTemplate.transform.parent, false);

            testHistoryList.Add(newListItem);
        }
    }

    public void TestButton_Click()
    {
        /*
        for (int i = 0; i < 3; i++)
        {
            GameObject newLogItem = Instantiate(logItemTemplate) as GameObject;
            newLogItem.SetActive(true);
            newLogItem.GetComponent<TestHistoryListItem>().setText("test log item button " + i);
            newLogItem.transform.SetParent(logItemTemplate.transform.parent, false);

            testHistoryLogItemList.Add(newLogItem);
        }
        */
    }

    public void displayTest(TestInfo ti)
    {
        if (ti.eyeMap == null)
        {
            Debug.Log("generating eyemap for list item prior to displaying test...");
            ti.generateEyeMap();
        }

        Debug.Log("setting test results image to sprite ID " + ti.spriteID);
        testResultsImage.overrideSprite = ti.eyeMapSprite;//Sprite.Create(ti.eyeMap, new Rect(0, 0, ti.eyeMap.width, ti.eyeMap.height), Vector2.zero);

        currentlySelectedTest = ti;
    }

    void Update()
    {
        //
    }

    public void BackButton_Click()
    {
        main.setActivePanel(UIPanel.MainMenu);
    }

    public void SaveToImageButton_Click()
    {
        if (currentlySelectedTest != null)
        {
            Texture2D temp = new Texture2D(2, 2);
            temp.SetPixels32(currentlySelectedTest.eyeMap.GetPixels32());
            temp.Resize(currentlySelectedTest.eyeMap.width * 20, currentlySelectedTest.eyeMap.height * 20);
            temp.Apply();
            Debug.Log("saving test results to image...");
            NativeGallery.SaveImageToGallery(temp, "SmartHVF", 
                currentlySelectedTest.patientID + "-" + currentlySelectedTest.dateTime.ToString("yyyy-MMM-dd-HH-mm-ss") + ".png");
            UnityEngine.Object.Destroy(temp);
        }
    }
}
