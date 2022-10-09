using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Xml;
using UnityEngine;
using SimpleFileBrowser;

// the goal for this class is to encapsulate all relevant data for a given test.
// which eye was tested, the color/background settings, type of test (24-2, for example)
// the parameters of the stimulus presentation algorithm (TBD), the associated Patient
// object, the actual stimulus field values, etc.
//
// each instance of this class is associated with a Patient object, via both the reference to
// the actual object and the patient ID.  some class members are serialized and save to
// XML as noted below with the attributes.  

// simple enum for a type of test.  probably expand this later to include stuff like 24-2,
// or other field patterns?
public enum TestType
{
    LeftEye,
    RightEye
}

// attributes to allow serializing to XML
[DataContract(Name = "TestInfo")]
public class TestInfo
{
    [DataMember(Name = "TestType")]
    public TestType type;
    [DataMember(Name = "GoldmannSize")]
    public GoldmannSize stimulusSize;
    [DataMember(Name = "DateTime")]
    public DateTime dateTime;
    [DataMember(Name = "Duration")]
    public int duration;
    [DataMember(Name = "PatientID")]
    public string patientID;
    public Patient patient;
    [DataMember(Name = "CamOrthoSize")]
    public float camOrthoSize;
    [DataMember(Name = "StepSize")]
    public float stepSize;

    [DataMember(Name = "StimulusField")]
    public List<Stimulus> stimulusField;
    public List<Stimulus> shuffledField;
    public GameObject stimulusPrefab;
    [DataMember(Name = "FieldBoundsMin")]
    public Vector3 stimulusFieldBoundsMin;
    [DataMember(Name = "FieldBoundsMax")]
    public Vector3 stimulusFieldBoundsMax;
    public Texture2D eyeMap;
    public Sprite eyeMapSprite;
    public int spriteID;

    public static Texture2D eyeMapMaskLeft, eyeMapMaskRight;

    // need at least a test type, a patient ref, and a cam's ortho size to make a new instance.
    // goldmann size defaults to 3 for now.
    public TestInfo(TestType type, Patient patient, float camOrthoSize, GoldmannSize stimulusSize = GoldmannSize.III)
    {
        this.type = type;
        this.patient = patient;
        this.patientID = patient.patientID;
        this.camOrthoSize = camOrthoSize;
        this.stimulusSize = stimulusSize;
        this.eyeMap = null;
        this.eyeMapSprite = null;

        buildStimulusField();
    }

    ~TestInfo()
    {
        // WHY aren't these destroying properly?  the game objects are still in
        // play while running this in game mode in the editor!
        //Debug.Log("~TestInfo() called");
        if (stimulusField != null)
        {
            //Debug.Log("Destroying stimulus field, count: " + stimulusField.Count);
            foreach (Stimulus s in stimulusField)
                s.destroy();
            stimulusField.Clear();
        }
        if (eyeMap != null)
            UnityEngine.Object.Destroy(eyeMap);
        if (eyeMapSprite != null)
            UnityEngine.Object.Destroy(eyeMapSprite);
    }

    private void buildStimulusField()
    {
        // note to self, default cam stereo separation is 0.022
        Debug.Log("building stimulus field in 3D...");

        this.stimulusField = new List<Stimulus>();
        this.stimulusPrefab = GameObject.Find("AppControl").GetComponent<Main>().stimulusPrefab;

        // the length of the rows of stimuli in a 24-2 test
        int[] rowLengths = { 4, 6, 8, 10, 10, 8, 6, 4 };

        // stepSize will be the angular step as the stimuli as placed across the hemisphere.
        // there are 24 degrees from the central axis to one edge of the visual field tested,
        // thus there are 3.5 steps from the central axis to the edge.  the 2 stimuli at the
        // nasal bulge will go 1 step further
        this.stepSize = 24.0f / 3.5f;

        // x/y loop to generate the roundish/diamond pattern
        //                  <---theta--->
        //
        //                  x   x   x   x
        //         -24                         +24
        //          |   x   x   x   x   x   x   |         ----- +24
        //
        //          x   x   x   x   x   x   x   x
        //                                                   A
        //      R   x   x   x   x   x   x   x   x   L        |
        //                                                  phi
        //      R   x   x   x   x   x   x   x   x   L        |
        //                                                   V
        //          x   x   x   x   x   x   x   x
        //
        //              x   x   x   x   x   x             ----- -24
        //
        //                  x   x   x   x
        //
        // the stimuli marked R are skipped for a left visual field, and vice versa for L

        Vector3 pos;
        Vector3 pos2D = Vector3.zero;
        Vector3 target = new Vector3();
        float theta, phi;
        float r = 4.2f;
        Vector3 camPos = GameObject.Find("Main Camera").transform.position;
        Debug.Log("cam pos: " + camPos);

        for (int y = 0; y < rowLengths.Length; ++y)
        {
            for (int x = 0; x < rowLengths[y]; ++x)
            {
                // compute the position of the stimulus field projected along a hemisphere
                theta = (-(float)(rowLengths[y] - 1) / 2.0f * this.stepSize + (float)x * this.stepSize) * Mathf.PI / 180.0f;
                phi = (-(float)(rowLengths.Length - 1) / 2.0f * this.stepSize + (float)y * this.stepSize) * Mathf.PI / 180.0f;

                pos.x = r * Mathf.Sin(theta);
                pos.y = r * Mathf.Sin(phi);
                pos.z = r * Mathf.Cos(theta) * Mathf.Cos(phi);

                // also compute a regular 2D version used for sampling
                pos2D.x = -(float)(rowLengths[y] - 1) / 2.0f + (float)x;
                pos2D.y = -(float)(rowLengths.Length - 1) / 2.0f + (float)y;
                pos2D.z = 0;
                

                //Debug.Log("coord: " + pos);

                // this will generate a look vector that is opposite of what would normally be used.
                // for whatever reason, a quad that is facing/looking away from the camera is visible?
                target = pos - camPos;
                
                // add the two extra stimuli to the central 2 rows based on which eye the field is built for
                if (y == 3 || y == 4)
                {
                    // left eye test has extra stimuli on the right, so skip the left ones
                    if (this.type == TestType.LeftEye && x == 0)
                        continue;
                    // and vice versa
                    else if (this.type == TestType.RightEye && x == 9)
                        continue;
                }

                this.stimulusField.Add(new Stimulus(this.stimulusPrefab, pos, pos2D, target, this.stimulusSize));
            }
        }

        // find the extents of the stimulus field in world space.
        // these bounds are used by the sampling algorithm to generate a finer-grained eyemap
        this.stimulusFieldBoundsMin = new Vector3(float.MaxValue, float.MaxValue, 0);
        this.stimulusFieldBoundsMax = new Vector3(float.MinValue, float.MinValue, 0);

        foreach (Stimulus s in this.stimulusField)
        {
            if (s.position2D.x < this.stimulusFieldBoundsMin.x)
                this.stimulusFieldBoundsMin.x = s.position2D.x;
            if (s.position2D.y < this.stimulusFieldBoundsMin.y)
                this.stimulusFieldBoundsMin.y = s.position2D.y;

            if (s.position2D.x > this.stimulusFieldBoundsMax.x)
                this.stimulusFieldBoundsMax.x = s.position2D.x;
            if (s.position2D.y > this.stimulusFieldBoundsMax.y)
                this.stimulusFieldBoundsMax.y = s.position2D.y;
        }

        // expand the bounds by half a unit in each direction
        this.stimulusFieldBoundsMin.x -= 0.5f;
        this.stimulusFieldBoundsMin.y -= 0.5f;
        this.stimulusFieldBoundsMax.x += 0.5f;
        this.stimulusFieldBoundsMax.y += 0.5f;
  

        // build a second list that is a shuffled version of the first
        this.shuffledField = new List<Stimulus>(this.stimulusField.Count);

        // list of indices to randomly pick from
        List<int> indexList = new List<int>(this.stimulusField.Count);
        for (int i = 0; i < this.stimulusField.Count; i++)
            indexList.Add(i);

        // rng
        System.Random rng = new System.Random();

        // randomly pick and remove an index to from the list and add
        // that corresponding stimulus to the shuffled list
        while (indexList.Count > 0)
        {
            int index = rng.Next(0, indexList.Count);
            this.shuffledField.Add(this.stimulusField[indexList[index]]);
            indexList.RemoveAt(index);
        }
    }

    private void buildStimulusField2D()
    {
        /*
        this.stimulusField = new List<Stimulus>();
        this.stimulusPrefab = GameObject.Find("AppControl").GetComponent<Main>().stimulusPrefab;

        // generates the approximate stimulus pattern from an actual HVF 24-2 test.
        int[] rowLengths = { 4, 6, 8, 10, 10, 8, 6, 4 };

        // used to adjust the overall scale of the field.  so far 1.0 is fine
        float fieldScale = 1.0f;
        // stepSize represents the worldspace distance between neighboring stimuli
        this.stepSize = this.camOrthoSize * 2.0f / (rowLengths.Length + 1) * fieldScale;

        // x/y loop to generate the roundish/diamond pattern
        //                  x   x   x   x
        //
        //              x   x   x   x   x   x
        //
        //          x   x   x   x   x   x   x   x
        //
        //      R   x   x   x   x   x   x   x   x   L
        //
        //      R   x   x   x   x   x   x   x   x   L
        //
        //          x   x   x   x   x   x   x   x
        //
        //              x   x   x   x   x   x
        //
        //                  x   x   x   x
        //
        // the stimuli marked R are skipped for a left visual field, and vice versa for L

        Vector3 pos = Vector3.zero;

        for (int y = 0; y < rowLengths.Length; ++y)
        {
            for (int x = 0; x < rowLengths[y]; ++x)
            {
                pos.x = -(float)(rowLengths[y] - 1) / 2.0f * this.stepSize + (float)x * this.stepSize;
                pos.y = -(float)(rowLengths.Length - 1) / 2.0f * this.stepSize + (float)y * this.stepSize;
                // default z plane for the stimulus field is -15.  during the test, they're
                // moved to z = 0
                pos.z = Stimulus.inactiveZPlane;

                // add the two extra stimuli to the central 2 rows based on which eye the field is built for
                if (y == 3 || y == 4)
                {
                    // left eye test has extra stimuli on the right, so skip the left ones
                    if (this.type == TestType.LeftEye && x == 0)
                        continue;
                    // and vice versa
                    else if (this.type == TestType.RightEye && x == 9)
                        continue;
                }

                this.stimulusField.Add(new Stimulus(this.stimulusPrefab, pos, -Vector3.forward, this.stimulusSize));
            }
        }

        // find the extents of the stimulus field in world space.
        // these bounds are used by the sampling algorithm to generate a finer-grained eyemap
        this.stimulusFieldBoundsMin = new Vector3(float.MaxValue, float.MaxValue, 0);
        this.stimulusFieldBoundsMax = new Vector3(float.MinValue, float.MinValue, 0);

        foreach (Stimulus s in this.stimulusField)
        {
            if (s.position.x < this.stimulusFieldBoundsMin.x)
                this.stimulusFieldBoundsMin.x = s.position.x;
            if (s.position.y < this.stimulusFieldBoundsMin.y)
                this.stimulusFieldBoundsMin.y = s.position.y;

            if (s.position.x > this.stimulusFieldBoundsMax.x)
                this.stimulusFieldBoundsMax.x = s.position.x;
            if (s.position.y > this.stimulusFieldBoundsMax.y)
                this.stimulusFieldBoundsMax.y = s.position.y;
        }

        // expand the bounds by half the step size in each direction
        this.stimulusFieldBoundsMin.x -= (this.stepSize / 2.0f);
        this.stimulusFieldBoundsMin.y -= (this.stepSize / 2.0f);
        this.stimulusFieldBoundsMax.x += (this.stepSize / 2.0f);
        this.stimulusFieldBoundsMax.y += (this.stepSize / 2.0f);

        

        // build a second list that is a shuffled version of the first
        this.shuffledField = new List<Stimulus>(this.stimulusField.Count);

        // list of indices to randomly pick from
        List<int> indexList = new List<int>(this.stimulusField.Count);
        for (int i = 0; i < this.stimulusField.Count; i++)
            indexList.Add(i);
        
        // rng
        System.Random rng = new System.Random();

        // randomly pick and remove an index to from the list and add
        // that corresponding stimulus to the shuffled list
        while (indexList.Count > 0)
        {
            int index = rng.Next(0, indexList.Count);
            this.shuffledField.Add(this.stimulusField[indexList[index]]);
            indexList.RemoveAt(index);
        }

        //Debug.Log("stimulus field size: " + this.stimulusField.Count);
        //Debug.Log("shuffled field size: " + this.shuffledField.Count);
        */
    }

    // move the whole stimulus field to the inactive Z plane (behind the camera)
    public void hideStimulusField()
    {
        foreach (Stimulus s in this.stimulusField)
            s.hide();
    }

    // this function is used in generating the grayscale map.
    private float sampleStimulusField(Vector3 pos)
    {
        // construct a list of tuples, storing a reference to a stimulus and the distance from pos
        List<Tuple<Stimulus, float>> distanceList = new List<Tuple<Stimulus, float>>();

        foreach (Stimulus s in this.stimulusField)
        {
            float d = Vector3.Distance(pos, s.position2D);
            distanceList.Add(new Tuple<Stimulus, float>(s, d));
        }

        // ascending sort based on distance (item2 of the tuple)
        distanceList.Sort((x, y) => x.Item2.CompareTo(y.Item2));

        // sample only stimuli within a certain radius based on stepsize.
        // the scale factor on stepsize is sqrt(2)/2 * 1.1, which is
        // 10% larger than the radius of a circle that circumscribes a
        // square of side length stepsize.
        float sample = 0;
        int sampleCount = 0;
        foreach (var t in distanceList)
        {
            //if (t.Item2 <= (this.stepSize * 0.7778f))
            if (t.Item2 <= 0.7778f)     // stepSize is now 1 in the 2D field
            {
                // inverting the brightness/threshold for the grayscale map.
                // if a bright stimulus isn't seen, then that deficiency shows
                // up as a dark region on the map.
                sample += (1.0f - t.Item1.brightness);
                sampleCount++;
            }
        }

        // just average however many stimuli were in range of sampling
        if (sampleCount > 0)
            return sample / sampleCount;
        // if somehow none were sampled, just pick the closest stimulus
        else
            return 1.0f - distanceList[0].Item1.brightness;
    }

    // build the higher resolution "grayscale map" from the lower resolution
    // field of stimuli by sampling through the field at a finer resolution.
    // the points sampled are determined by a "mask" defined in two separate
    // .png files under /Resources (one for each eye).  the center of each
    // pixel is used to sample the stimulus field in worldspace and generate
    // a brightness value.
    public void generateEyeMap()
    {
        //string filePath = (this.type == TestType.LeftEye) ? "eyemap_left" : "eyemap_right";
        Color[] pixelData;

        // maintain static copies of each eyemask in the class.  repeated calls to Resources.Load
        // will return the same reference to one object (oops)
        eyeMapMaskLeft = Resources.Load<Texture2D>("eyemap_left");
        eyeMapMaskRight = Resources.Load<Texture2D>("eyemap_right");

        // duplicate the needed texture for each TestInfo instance
        if (this.type == TestType.LeftEye)
        {
            this.eyeMap = new Texture2D(eyeMapMaskLeft.width, eyeMapMaskLeft.height);
            this.eyeMap.SetPixels32(eyeMapMaskLeft.GetPixels32());
        }
        else
        {
            this.eyeMap = new Texture2D(eyeMapMaskRight.width, eyeMapMaskRight.height);
            this.eyeMap.SetPixels32(eyeMapMaskRight.GetPixels32());
        }

        if (this.eyeMap != null)
        {
            // since this same texture is used to fill in the Image UI element
            // in the TestResultsPanel, turn off any filtering to keep the
            // map in its full blocky goodness.
            this.eyeMap.filterMode = FilterMode.Point;

            Debug.Log("texture size: " + this.eyeMap.width + " w x " + this.eyeMap.height + " h");
            // dump the rgba data into a buffer
            pixelData = this.eyeMap.GetPixels();

            // the step size to move for each sample, in world coords
            float mapStepX = (float)(this.stimulusFieldBoundsMax.x - this.stimulusFieldBoundsMin.x) / this.eyeMap.width;
            float mapStepY = (float)(this.stimulusFieldBoundsMax.y - this.stimulusFieldBoundsMin.y) / this.eyeMap.height;

            // start at the bottom left corner of the field bounds (biased to the center of the sample)
            Vector3 p = new Vector3();
            p.x = this.stimulusFieldBoundsMin.x + mapStepX / 2.0f;
            p.y = this.stimulusFieldBoundsMin.y + mapStepY / 2.0f;
            p.z = 0;// Stimulus.inactiveZPlane;

            // iterate through the eyemap mask, using the alpha channel to tell where to sample (a == 1).
            // after sampling, write the value into the RGB portion.
            for (int y = 0; y < this.eyeMap.height; ++y)
            {
                // at the start of each row, reset x coord to the left
                p.x = this.stimulusFieldBoundsMin.x + mapStepX / 2.0f;

                for (int x = 0; x < this.eyeMap.width; ++x)
                {
                    int index = x + y * this.eyeMap.width;
                    // only sample where alpha == 1
                    if (pixelData[index].a == 1.0f)
                    {
                        float sample = sampleStimulusField(p);
                        if (sample <= 0)
                            sample = 5.0f / 255.0f;
                        pixelData[index].r = pixelData[index].g = pixelData[index].b = sample;
                    }

                    // move x coord over
                    p.x += mapStepX;
                }

                // move y coord up
                p.y += mapStepY;
            }

            // upload buffer to the texture
            this.eyeMap.SetPixels(pixelData);
            this.eyeMap.Apply();

            this.eyeMapSprite = Sprite.Create(this.eyeMap, new Rect(0, 0, this.eyeMap.width, this.eyeMap.height), Vector2.zero);
            System.Random rng = new System.Random();
            this.spriteID = rng.Next();
        }
        else
            Debug.Log("failed to load eyemap!");
    }

    private string getXMLDataString()
    {
        using (MemoryStream ms = new MemoryStream())
        {
            DataContractSerializer s = new DataContractSerializer(this.GetType());
            s.WriteObject(ms, this);
            ms.Seek(0, SeekOrigin.Begin);

            using (var sr = new StreamReader(ms))
            {
                string result = sr.ReadToEnd();
                return result;
            }
        }
    }

    public void testSave()
    {
        try
        {
            string path = Application.persistentDataPath + "/Patients/" + this.patient.patientID + "/" + this.dateTime.ToString("yyyy-MMM-dd-HH-mm-ss") + ".xml";
            Debug.Log("test save to " + path);

            FileBrowserHelpers.WriteTextToFile(path, this.getXMLDataString());

            Debug.Log("Wrote TestInfo object as serialized XML!");
        }
        catch (Exception e)
        {
            Debug.Log("Failed to save test results!  reason: " + e.Message);
        }
    }

    public static TestInfo loadFromFile(string path)
    {
        try
        {
            FileStream f = File.Open(path, FileMode.Open);
            XmlReader reader = XmlReader.Create(f);
            DataContractSerializer s = new DataContractSerializer(typeof(TestInfo));
            TestInfo ti = (TestInfo)s.ReadObject(reader, false);

            return ti;
        }
        catch (Exception e)
        {
            Debug.Log("Failed to read serialized TestInfo object!  reason: " + e.Message);

            return null;
        }
    }
}
