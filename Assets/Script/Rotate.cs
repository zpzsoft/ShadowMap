using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Rotate : MonoBehaviour {

    public int m_RotateSpeed = 1;

	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
        if (m_RotateSpeed <= 0) return;

        transform.RotateAround(Vector3.zero, Vector3.up, m_RotateSpeed);
	}
}
