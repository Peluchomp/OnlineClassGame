using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Obi;

public class RopeLengthController : MonoBehaviour
{

    public float speed = 1;
    ObiRopeCursor cursor;
    ObiRope rope;

    void Start()
    {
        cursor = GetComponentInChildren<ObiRopeCursor>();
        rope = cursor.GetComponent<ObiRope>();
    }

    void Update()
    {
        if (Input.GetKey(KeyCode.W))
            cursor.ChangeLength(-speed * Time.deltaTime);

        if (Input.GetKey(KeyCode.S))
            cursor.ChangeLength(speed * Time.deltaTime);
    }
}
