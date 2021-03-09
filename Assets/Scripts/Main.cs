using SimpleFileBrowser;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

// enum type to specify which UI panel is currently active/shown.
// the InTest state would have everything hidden while the test coroutine runs
public enum UIPanel
{
    MainMenu = 0, NewTestSetup, LoadPatient, BrowseTestHistory, TestResults, None
}

public enum TestState
{
    Inactive = 0, ReadyToStart, InProgress, WaitingToEnd
}

public class Main : MonoBehaviour
{
    // linked in the inspector
    public GameObject stimulusPrefab;
    public GameObject crosshair;
    public RenderTexture resultsTexture;

    public GameObject[] UIPanels;
    // sample object used to ensure headset is focused before a test starts
    public GameObject focusCube;
    private float focusCubeProgress;
    /*
    public GameObject mainMenuPanel;
    public GameObject testConfigPanel;
    public GameObject patientDataPanel;
    public GameObject testResultsPanel;
    public GameObject loadPatientPanel;
    */

    public Camera mainCamera;
    public GameObject testResultsPreviewBackdrop;

    // state variables for testing and input
    private TestState testState;
    private bool abortTest;
    private bool stimulusSeen;

    // used for the abort test functionality
    private float lastTouchStartTime;

    // simple timer to trigger on user timeout
    private TimeoutTimer tot;

    // used in generating the field and grayscale map
    private float camOrthoSize;
    private float stepSize;

    //[HideInInspector]
    public Patient currentPatient = null;
    //public TestInfo currentTestInfo = null;

    // java objects to interface with the SmartHVF-Input library for BT commss
    private AndroidJavaObject unityContext;
    private AndroidJavaObject btLib;

    private TestInfo testti;

    void Start()
    {
        XRSettings.enabled = false;
        XRDevice.DisableAutoXRCameraTracking(mainCamera, true);

        Debug.Log("Starting app ...");
        Debug.Log("Screen size: " + Screen.width + ", " + Screen.height);

        camOrthoSize = mainCamera.orthographicSize;

        Debug.Log("Camera ortho size: " + camOrthoSize);
        Debug.Log("World size: " + ((float)Screen.width / Screen.height * camOrthoSize * 2.0f) + ", " + (camOrthoSize * 2.0f));

        // set initial states
        testState = TestState.Inactive;
        abortTest = false;
        stimulusSeen = false;
        lastTouchStartTime = 0;

        // start app on the main menu panel
        setActivePanel(UIPanel.MainMenu);

        // create the timeout timer
        tot = new TimeoutTimer();

        
        // setup the Android Java objects that let us communicate to the SmartHVF-Input project and
        // receive bluetooth comms
        Debug.Log("Atempting to start BT library...");
        AndroidJavaClass player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        unityContext = player.GetStatic<AndroidJavaObject>("currentActivity");

        btLib = new AndroidJavaObject("com.example.testlibrary.TestClass");
        btLib.Call("InitBluetooth", new object[] { unityContext });
        
    }

    
    public void TestButton_Click()
    {
        Patient p = new Patient("bob", 24, "12345");
        testti = new TestInfo(TestType.LeftEye, p, 5.0f);

        mainCamera.stereoTargetEye = StereoTargetEyeMask.Left;
        XRSettings.enabled = true;


        //mainMenuPanel.GetComponent<CanvasRenderer>().cull = true;

        //FileBrowser.ShowLoadDialog(TestOnSuccess, TestOnCancel, false, false, Application.persistentDataPath);
        /*
        string path = Application.persistentDataPath;
        string cwd = Directory.GetCurrentDirectory();
        string[] dirs = Directory.GetDirectories(cwd);
        string[] files = Directory.GetFiles(cwd);

        Debug.Log("Current working directory: " + cwd);
        Debug.Log("Directories:");
        foreach (string d in dirs)
            Debug.Log(d);
        Debug.Log("Files:");
        foreach (string f in files)
            Debug.Log(f);
        */
    }

    public void setActivePanel(UIPanel newState)
    {
        // hide any active UI panels
        foreach (GameObject o in UIPanels)
            o.SetActive(false);

        // activate the requested one
        switch (newState)
        {
            case UIPanel.MainMenu:          UIPanels[0].SetActive(true); break;
            case UIPanel.NewTestSetup:      UIPanels[1].SetActive(true); break;
            case UIPanel.LoadPatient:       UIPanels[2].SetActive(true); break;
            case UIPanel.BrowseTestHistory: UIPanels[3].SetActive(true); break;
            case UIPanel.TestResults:       UIPanels[4].SetActive(true); break;

            case UIPanel.None: break;     // no UI panels visible during test routine
        }
    }

    // Update is called once per frame
    void Update()
    {    
        // detect any touching of the screen
        if (Input.touchCount > 0)
        {
            // only 1 finger
            if (Input.touchCount == 1)
            {
                Touch touch = Input.GetTouch(0);

                //Debug.Log("touch received");

                if (testState == TestState.ReadyToStart)
                {
                    if (touch.phase == TouchPhase.Began)
                    {
                        lastTouchStartTime = Time.time;
                        focusCubeProgress = 1.0f;
                    }
                    else if (touch.phase == TouchPhase.Stationary)
                    {
                        float dt = Time.time - lastTouchStartTime;
                        if (dt > 3.0f)
                        {
                            lastTouchStartTime = Time.time;
                            testState = TestState.InProgress;
                        }
                        else
                        {
                            focusCubeProgress = 1.0f - dt / 3.0f;
                            // initial focus cube scale is 1,1,1 and rotation is -45, -30, 15.
                            // scale this down to 0,0,0 and 0,0,0 as the button is held
                            focusCube.transform.localScale = new Vector3(focusCubeProgress, focusCubeProgress, focusCubeProgress);
                            focusCube.transform.localRotation = Quaternion.Euler(-45.0f * focusCubeProgress, -30.0f * focusCubeProgress, -15.0f * focusCubeProgress);
                        }
                    }
                    else if (touch.phase == TouchPhase.Ended)
                    {
                        resetFocusCube();
                    }
                }
                else if (testState == TestState.InProgress)
                {
                    /*
                    // if there was data received over BT, count that too
                    if (btLib.Call<bool>("GetInput") == true)
                        stimulusSeen = true;
                        */


                    // only respond to the initial touch
                    if (touch.phase == TouchPhase.Began)
                    {
                        lastTouchStartTime = Time.time;
                        //Debug.Log("Touch input received...");
                        stimulusSeen = true;
                    }
                    // else, if user holds for 3 seconds, abort the test
                    else if (touch.phase == TouchPhase.Stationary && ((Time.time - lastTouchStartTime) > 3.0f))
                    {
                        lastTouchStartTime = Time.time;
                        //Debug.Log("in stationary touch (last time: " + lastTouchStartTime.ToString("F1") + ", current time: " + Time.time.ToString("F1") + ")");
                        abortTest = true;
                    }
                }
            }
        }

        

        // keep the timeout timer updated if in testing
        if (testState == TestState.InProgress)
            tot.update();
    }


    // a bootstrap function to start the Co-routine.  during yields, it'll come back to this script
    // which is always active (whereas UI panel scripts are active/inactive at various times)
    public void startTest(TestInfo testInfo)
    {
        StartCoroutine(fieldTest2(testInfo));
    }

    private void resetFocusCube()
    {
        focusCubeProgress = 1.0f;
        focusCube.transform.localScale = new Vector3(1, 1, 1);
        focusCube.transform.localRotation = Quaternion.Euler(-45.0f, -30.0f, -15.0f);
    }

    // the main test routine!
    public IEnumerator fieldTest2(TestInfo testInfo)
    {
        bool inRampDown;
        
        // hide all UI panels
        setActivePanel(UIPanel.None);

        testInfo.dateTime = System.DateTime.Now;
        int startTime = (int)Time.time;

        Debug.Log("Test ready to start...");
        //Debug.Log("Test info: " + testInfo.type + ", stimulus size: " + testInfo.stimulusSize + ", datetime: " + testInfo.dateTime.ToString("yyyyMMdd-HH-mm-ss"));

        // wait for 1 second before beginning test
        yield return new WaitForSeconds(0.5f);

        Debug.Log("enabling VR");
        mainCamera.stereoTargetEye = (testInfo.type == TestType.LeftEye ? StereoTargetEyeMask.Left : StereoTargetEyeMask.Right);
        //mainCamera.orthographic = false;
        XRSettings.enabled = true;

        yield return new WaitForSeconds(0.5f);

        // move focus into view so it can be used to ensure headset is focused and clear
        focusCube.transform.position = new Vector3(0, 0, 3);

        // enter the ready state and wait until for a 3 second hold
        testState = TestState.ReadyToStart;

        yield return new WaitUntil(() => (testState == TestState.InProgress));

        // move the crosshair into view
        crosshair.GetComponent<Transform>().SetPositionAndRotation(new Vector3(0, 0, 6), Quaternion.identity);

        Debug.Log("Starting test...");

        abortTest = false;

        yield return new WaitForEndOfFrame();

        yield return new WaitForSeconds(3.0f);

        // for testing eyemap generation
        bool debugMode = false;

        // iterate through stimuli

        if (debugMode)
        {
            System.Random rng = new System.Random();

            foreach (Stimulus s in testInfo.shuffledField)
            {
                s.dimBy((float)rng.NextDouble());
                s.show();
            }

            stimulusSeen = false;
            yield return new WaitUntil(() => stimulusSeen);

            testInfo.hideStimulusField();
        }
        else
        {
            testInfo.hideStimulusField();

            Stimulus s;
            //for (int i = 0; i < 5 && !abortTest; ++i)
            for (int i = 0; i < testInfo.shuffledField.Count && !abortTest; ++i)
            {
                s = testInfo.shuffledField[i];
                // the ramp down is the steady decrease in brightness for a given stimulus until it's no longer visible
                inRampDown = true;
                while (inRampDown)
                {
                    // reset this each loop
                    stimulusSeen = false;

                    // show the stimulus at its current brightness
                    s.show();

                    // clear the input status in the Java BT library
                    //btLib.Call("ClearInput");

                    // wait for 200ms
                    yield return new WaitForSeconds(0.2f);

                    // hide it
                    s.hide();

                    // start the timeout timer for 3 seconds
                    tot.start(3.0f);

                    // wait until the user indicates stimulus was seen, the timer times out, or the user aborts the test
                    yield return new WaitUntil(() => (stimulusSeen || tot.timeout || abortTest));

                    if (stimulusSeen)
                    {
                        //Debug.Log("stimulus seen");
                        // decrease by 10%
                        s.dimBy(0.1f);
                        //s.brightness = 0.0f;
                        //inRampDown = false;
                        // brief delay before next round
                        yield return new WaitForSeconds(0.4f);

                    }
                    else if (tot.timeout)
                    {
                        //Debug.Log("input timeout");
                        // we've hit the brightness threshold for this stimulus, so end this loop and start on next stimulus
                        inRampDown = false;

                    }
                    else if (abortTest)
                    {
                        Debug.Log("Aborting test...");

                        stimulusSeen = false;
                        testState = TestState.Inactive;
                        //abortTest = false;


                        //crosshair.GetComponent<Transform>().SetPositionAndRotation(new Vector3(0, 0, -5.0f), Quaternion.identity);
                        //testConfigPanel.SetActive(true);
                        break;
                    }
                }
            }
        }

        Debug.Log("done");

        // move crosshair behind the camera
        crosshair.GetComponent<Transform>().SetPositionAndRotation(new Vector3(0, 0, -5.0f), Quaternion.identity);
        // move focus cube behind the camera
        focusCube.transform.position = new Vector3(0, 0, -3);
        // reset its original scale/rotation
        resetFocusCube();

        Debug.Log("disabling VR");
        mainCamera.stereoTargetEye = StereoTargetEyeMask.None;
        //mainCamera.orthographic = false;
        XRSettings.enabled = false;

        yield return new WaitForSeconds(0.5f);


        // at this point, the stimulus objects contain the brighness values at the threshold of visibility

        // if test wasn't aborted prematurely, then wrap things up
        if (!abortTest)
        {
            testInfo.duration = (int)Time.time - startTime;
            Debug.Log("Test complete");

            stimulusSeen = false;
            testState = TestState.Inactive;
            abortTest = false;

            // build the eyemap and transition to the test results panel
            testInfo.generateEyeMap();

            UIPanels[(int)UIPanel.TestResults].GetComponent<TestResultsPanelControl>().lastTest = testInfo;
            setActivePanel(UIPanel.TestResults);
        }
        // else flee back to the test setup panel
        else
            setActivePanel(UIPanel.NewTestSetup);
    }

    public void testSave()
    {
        // make all stimuli visible and move them behind main camera to z = -15.0f.
        // this is in between the "backdrop" quad (z = -10) and the results camera (z = -20)
        //hideStimulusField();

        // coroutine finishes the job
        StartCoroutine(CoTestSave());
        //generateEyeMap();
        //sampleStimulusField(Vector3.zero);
    }

  
    private IEnumerator CoTestSave()
    {
        // wait until end of frame so we know all stimuli are on and have been moved
        yield return new WaitForEndOfFrame();

        Debug.Log("Saving results to SmartHVF gallery...");

        Debug.Log("position of crosshair: " + crosshair.transform.position);
        Debug.Log("position of results backdrop: " + GameObject.Find("Results Backdrop").transform.position);
        crosshair.SetActive(false);
        testResultsPreviewBackdrop.SetActive(false);
        yield return new WaitForEndOfFrame();
        // set the currently active render texture to the one used by the results camera
        RenderTexture.active = resultsTexture;
        // create a temporary texture2d to copy this data into
        Texture2D temp = new Texture2D(resultsTexture.width, resultsTexture.height);

        temp.ReadPixels(new Rect(0, 0, resultsTexture.width, resultsTexture.height), 0, 0);
        temp.Apply();
        RenderTexture.active = null;
        crosshair.SetActive(true);
        testResultsPreviewBackdrop.SetActive(true);

        string nowString = System.DateTime.Now.ToString("yyyyMMdd-HH-mm-ss");
        // this plugin takes a texture2D, encodes to a .png image, and saves it to the gallery
        NativeGallery.SaveImageToGallery(temp, "SmartHVF", nowString + "-field.png");
        

        /*
        // simple blocky eyemap generation.
        // blockSize is computed as the ratio between stepSize and the total vertical extent in world space, but mapped to screen/pixel space
        int blockSize = (int)(stepSize / (camOrthoSize * 2.0f) * Screen.height);
        Color[] cols = new Color[blockSize * blockSize];

        foreach (Stimulus s in stimulusField)
        {
            // map the world coords to screen space
            Vector3 screenPos = mainCamera.WorldToScreenPoint(s.position);
            
            // really need some kind of memset equivalent here
            for (int i = 0; i < cols.Length; ++i)
                cols[i] = new Color(1.0f - s.brightness, 1.0f - s.brightness, 1.0f - s.brightness);

            // draw a single colored block centered at the stimulus location
            temp.SetPixels((int)screenPos.x - (blockSize - 1) / 2, (int)screenPos.y - (blockSize - 1) / 2, blockSize, blockSize, cols, 0);
        }

        temp.Apply();
        NativeGallery.SaveImageToGallery(temp, "SmartHVF", nowString + "-map1.png");
        */

        // v2 of eyemap sampling
        //Texture2D map2 = testInfo.generateEyeMap();
        //NativeGallery.SaveImageToGallery(map2, "SmartHVF", nowString + "-map2.png");

        // get rid of textures
        //Destroy(temp);
        //Destroy(map2);

        //hideStimulusField();
        
    }
}
