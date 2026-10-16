using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ShadowTheater.Data;
using ShadowTheater.Field;
using ShadowTheater.Save;
using ShadowTheater.UI;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ShadowTheater.Story
{
    public class TownNpcPatrol : MonoBehaviour
    {
        [SerializeField,Range(1,3)]private int requiredStage=2;
        [SerializeField]private Vector2 localOffset=new Vector2(2f,0);
        [SerializeField,Min(.1f)]private float speed=.65f;
        private Vector3 _origin;private bool _enabled;
        private void Awake()=>_origin=transform.localPosition;
        public void SetRestorationStage(int stage)=>_enabled=stage>=requiredStage;
        private void Update(){if(!_enabled)return;float wave=(Mathf.Sin(Time.time*speed)+1f)*.5f;transform.localPosition=_origin+(Vector3)(localOffset*wave);}
    }
}
