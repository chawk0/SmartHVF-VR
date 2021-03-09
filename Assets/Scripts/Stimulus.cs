using System;
using System.Runtime.Serialization;
using UnityEngine;
using UnityEngine.Assertions.Must;

// this object represents one stimulus point in the stimulus field, and it's
// represented as a quad in world space, textured with a white circle.
// the _Color shader parameter is varied to change "brightness" and the scale
// is manipulated to adjust the size, corresponding to Goldmann sizes I-V (needs to be calibrated)

public enum GoldmannSize
{
    I = 0, II, III, IV, V
}

// attributes for serializing to XML
[DataContract(Name = "Stimulus")]
public class Stimulus
{
    [DataMember(Name = "Pos")]
    public Vector3 position;
    [DataMember(Name = "Pos2D")]
    public Vector3 position2D;
    [DataMember(Name = "Orientation")]
    public Quaternion orientation;
    [DataMember(Name = "Brightness")]
    public float brightness;
    [DataMember(Name = "GoldmannSize")]
    public GoldmannSize size;

    private GameObject instance;
    private Material material;

    // defines the Z planes of the stimuli when part of an inactive test, and when hidden/inactive
    public const float inTestZPlane = 0.0f;
    public const float inactiveZPlane = -15.0f;
    
    public Stimulus(GameObject prefab, Vector3 startPosition, Vector3 position2D, Vector3 target, GoldmannSize size = GoldmannSize.III)
    {
        // initial position in worldspace
        this.position = startPosition;
        // 2D position used for sampling
        this.position2D = position2D;
        // create quat orientation based on look-at target (should be camera eye)
        Quaternion q = Quaternion.identity;
        q.SetLookRotation(target);
        this.orientation = q;
        // create instance from prefab
        //this.instance = (GameObject)GameObject.Instantiate(prefab, startPosition, Quaternion.identity);
        this.instance = (GameObject)MonoBehaviour.Instantiate(prefab, this.position, this.orientation);
        // grab a reference to the material for setting the color
        this.material = this.instance.GetComponent<Renderer>().material;
        // set to full brightness by default
        this.brightness = 1.0f;
        this.material.SetColor("_Color", new Color(this.brightness, this.brightness, this.brightness));
        // set scale based on Goldmann size
        this.size = size;
        computeScale(this.size);
        // set hidden/inactive by default
        //setZ(Stimulus.inactiveZPlane);
        hide();
    }

    ~Stimulus()
    {
        //Debug.Log("~Stimulus() invoked!");
        destroy();
    }

    // during testing, a stimulus is made visible by moving it to the active "in test" Z plane
    public void show()
    {
        //setZ(Stimulus.inTestZPlane);
        instance.SetActive(true);
    }

    // and hidden by moving it back to the inactive Z plane behind the camera
    public void hide()
    {
        //setZ(Stimulus.inactiveZPlane);
        instance.SetActive(false);
    }

    public void dimBy(float d)
    {
        this.brightness -= d;
        if (this.brightness < 0)
            this.brightness = 0;

        this.material.SetColor("_Color", new Color(this.brightness, this.brightness, this.brightness));
    }

    // this is not working.....?
    public void destroy()
    {
        //Debug.Log("Stimulus.destroy() invoked!");
        if (instance != null)
        {
            //Debug.Log("destroying stimulus instance: " + instance.name + ", object: " + instance);
            //UnityEngine.Object.Destroy(instance);
            //MonoBehaviour.Destroy(instance);
            UnityEngine.Object.Destroy(instance);
            //UnityEngine.Object.DestroyImmediate(instance);
            //Destroy(instance);
            instance = null;
        }
        else
            Debug.Log("can't destroy, stimulus instance null!");
    }

    // roughly calibrated using a tape measure and my pinky finger to work out an angular size
    // of an arbitrary size III stimulus, then scaled to the real size of a size III stimulus
    private void computeScale(GoldmannSize size)
    {
        // size I to V maps to 0 to 4.  each size is 4x the area as the previous, so x/y scale is doubled
        float newScale = 0.01f * (float)Math.Pow(2.0, (double)size);
        this.instance.transform.localScale = new Vector3(newScale, newScale, 1.0f); // scale only X and Y
    }

    /*
    // set both the prefab instance's transform, and the internal position
    private void setZ(float newZ)
    {
        this.instance.transform.SetPositionAndRotation(new Vector3(this.position.x, this.position.y, newZ), this.orientation);
        this.position.z = newZ;
    }
    */
}
