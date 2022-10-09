using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Runtime.Serialization;
using System.Xml;
using System.Xml.Schema;
using UnityEngine;
using SimpleFileBrowser;

// this class will hold some basic patient data, and also a list
// of tests already performed.

[DataContract(Name = "Patient")]  
public class Patient
{
    [DataMember(Name = "Name")]
    public string name;
    [DataMember(Name = "Age")]
    public int age;
    [DataMember(Name = "GUID")]
    public string guid;

    //[DataMember(Name = "TestHistory")]
    public List<TestInfo> testHistory;

    // this is combination of the first/last name and the first 8 digits of the GUID:
    // 'Joe_Bob_12345678'
    public string patientID;

    public Patient()
    {
        //
    }

    public Patient(string name, int age, string guid)
    {
        this.name = name;
        this.age = age;
        this.guid = guid;
        this.testHistory = new List<TestInfo>();

        this.patientID = null;
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

    public void saveToFile()
    {
        Debug.Log("saving new patient data!");
        // if patient name is "Joe Bob" and GUID is "09a669c5-....", then all
        // relevant information is stored in a directory named "Joe_Bob_09a669c5",
        // inside of which is "Joe_Bob_09a669c5.xml" as well as various test results
        // in separate .xml files.

        // retrieve the first chunk of the GUID
        string guidChunk = this.guid.Substring(0, this.guid.IndexOf('-'));
        // replace spaces with underscores and append the GUID chunk
        this.patientID = this.name.Replace(' ', '_') + "_" + guidChunk;

        Debug.Log("patient ID: " + this.patientID);

        try
        {
            // first check if the Patients directory exists
            string patientsDirectory = Application.persistentDataPath + "/Patients";
            bool patientsDirectoryExists = FileBrowserHelpers.DirectoryExists(patientsDirectory);

            if (!patientsDirectoryExists)
            {
                Debug.Log("patients directory doesn't exist!  creating [" + Application.persistentDataPath + "/Patients");
                FileBrowserHelpers.CreateFolderInDirectory(Application.persistentDataPath, "Patients");
            }
            else
                Debug.Log("found Patients directory");

            // next check if the new patient's data directory exists (should theoretically never happen)
            string patientDataDirectory = Application.persistentDataPath + "/Patients/" + this.patientID;
            //Debug.Log("patient data directory: " + patientDataDirectory);
            bool patientDataDirectoryExists = FileBrowserHelpers.DirectoryExists(patientDataDirectory);

            if (patientDataDirectoryExists)
                throw new DuplicateNameException("patient data directory already exists!");
            else
            {
                Debug.Log("creating patient data directory");
                FileBrowserHelpers.CreateFolderInDirectory(Application.persistentDataPath + "/Patients", this.patientID);

                string patientDataFile = patientDataDirectory + "/" + this.patientID + ".xml";
                Debug.Log("writing serialized patient data to " + patientDataFile);
                FileBrowserHelpers.WriteTextToFile(patientDataFile, this.getXMLDataString());
                Debug.Log("done");
            }
        }
        catch (Exception e)
        {
            Debug.Log("Failed to create patient data directory and/or xml file!  reason: " + e.Message);
        }
    }

    public static Patient readFromDirectory(string path)
    {
        Debug.Log("loading patient data from directory " + path);
        // path comes in as a full absolute path, i.e.:
        // /emulated/storage/..../com.USF...../Patients/Joe Bob-12345678
        try
        {
            // extract the patient id from the directory path
            string pid;

            // handle paths with both types of slashes
            char[] slashes = { '\\', '/' };
            int pos = path.LastIndexOfAny(slashes);
            
            if (pos != -1)
                pid = path.Substring(pos + 1);
            else
                throw new FileNotFoundException("malformed path");

            Debug.Log("patient ID: " + pid);

            FileStream f = File.Open(path + "/" + pid + ".xml", FileMode.Open);
            XmlReader reader = XmlReader.Create(f);
            DataContractSerializer s = new DataContractSerializer(typeof(Patient));
            Patient p = (Patient)s.ReadObject(reader, false);
            p.patientID = pid;
            p.loadTestHistory();

            f.Close();
            reader.Close();

            Debug.Log("Read Patient object as serialized XML!");

            return p;
        }
        catch (Exception e)
        {
            Debug.Log("Failed to read serialized Patient object!  reason: " + e.Message);

            return null;
        }
    }

    private void loadTestHistory()
    {
        Debug.Log("loading test history...");

        if (patientID.Length > 0)
        {
            if (testHistory != null)
                testHistory.Clear();

            string dataPath = Application.persistentDataPath + "/Patients/" + patientID;
            
            Debug.Log("reading directory for test files");
            //string[] files = Directory.GetFiles(Application.persistentDataPath + "/Patients/" + patientID, "*.xml");
            //string[] files = Directory.GetFiles(dataPath, "*.xml");
            FileSystemEntry[] files = FileBrowserHelpers.GetEntriesInDirectory(dataPath, false);
            Debug.Log("found " + files.Length + " files");

            testHistory = new List<TestInfo>();
            foreach (FileSystemEntry file in files)
            {
                Debug.Log("path: " + file.Path + ", name: " + file.Name + ", extension: " + file.Extension);
                // skip any directories or the main patient data .xml file
                if (!file.Name.Contains(patientID) && !file.IsDirectory)
                {
                    Debug.Log("loading test...");
                    TestInfo ti = TestInfo.loadFromFile(file.Path);
                    if (ti != null)
                    {
                        testHistory.Add(ti);
                        Debug.Log("testinfo loaded!  type: " + (ti.type == TestType.LeftEye ? "left" : "right") + ", size: " + (int)ti.stimulusSize + ", datetime: " + ti.dateTime + ", duration: " + ti.duration + ", patientid: " + ti.patientID);
                    }
                }
                /*
                string fileName;
                char[] slashes = { '\\', '/' };
                int pos = filePath.LastIndexOfAny(slashes);

                if (pos != -1)
                    fileName = filePath.Substring(pos + 1);
                else
                    throw new FileNotFoundException("malformed path");

                Debug.Log("checking file: " + fileName);
                // skip the actual patient .xml file
                if (!fileName.Contains(patientID))
                {
                    TestInfo ti = TestInfo.loadFromFile(filePath);
                    if (ti != null)
                    {
                        testHistory.Add(ti);
                        Debug.Log("testinfo loaded!  type: " + (ti.type == TestType.LeftEye ? "left" : "right") + ", size: " + (int)ti.stimulusSize + ", datetime: " + ti.dateTime + ", duration: " + ti.duration + ", patientid: " + ti.patientID);
                    }
                }
                */
            }
        }
    }
}
