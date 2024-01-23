using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if URP
using UnityEngine.Rendering.Universal;
#endif
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

namespace StylizedGrass
{
    [ExecuteInEditMode]
    [AddComponentMenu("Stylized Grass/Grass Bender")]
    [HelpURL(("http://staggart.xyz/unity/stylized-grass-shader/sgs-docs/?section=using-grass-bending"))]
    public class GrassBender : MonoBehaviour
    {
        [Tooltip("Higher layers are always drawn over lower layers. Use this to override other benders on a lower layer.")]
        [Range(0, 4)]
        public int sortingLayer = 0;

        //Common
		#pragma warning disable 108,114 //New keyword
        public Renderer renderer;
		#pragma warning restore 108,114
        
        [Range(-1, 1f)]
        public float heightOffset = 0f;
        [Range(0f, 3f)]
        public float flattenStrength = 1f;
        [Range(0, 3f)]
        public float pushStrength = 1f;
        [Range(0.1f, 3f)]
        public float scaleMultiplier = 1f;

        [Tooltip("When enabled, overlapping benders of the same type will blend together")]
        public bool alphaBlending = false;

        //Mesh
        public MeshFilter meshFilter;

        //Trail
        public TrailRenderer trailRenderer;
        [FormerlySerializedAs("forceUpdating")]
        [Tooltip("Jitter the position of the trail, to force the mesh to constantly update")]
        public bool forceTrailUpdating;
        
        //Line
        public LineRenderer lineRenderer;

        private MaterialPropertyBlock _props;
        public MaterialPropertyBlock props
        {
            get
            {
                //Fetch when required, execution order makes it unreliable otherwise
                if (_props == null)
                {
                    _props = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(_props);
                }
                return _props;
            }
        }
        
        private Material material;
        //Reference it, so that it's included in a build
        [SerializeField]
        private Shader bendingShader;

        private void Reset()
        {
            bendingShader = Shader.Find(BEND_SHADER_NAME);
        }

        public void OnEnable()
        {
            GetRenderer();
            
            if (trailRenderer)
            {
                //Updating to 1.3.0+
                trailRenderer.forceRenderingOff = false;
                trailRenderer.emitting = true;
                
                //Required for tangents
                trailRenderer.generateLightingData = true;
            }

            if (lineRenderer)
            {
                //Updating to 1.3.0+
                lineRenderer.forceRenderingOff = false;
                lineRenderer.generateLightingData = true;
            }
            
            //Updating to 1.3.0+
            bendingShader = Shader.Find(BEND_SHADER_NAME);
            
            SetupMaterial();
            UpdateProperties();
        }

        public static bool SRPBatcherEnabled()
        {
            #if URP
            return UniversalRenderPipeline.asset && UniversalRenderPipeline.asset.useSRPBatcher;
            #else
            return false;
            #endif
        }
        
        private void SetupMaterial()
        {
            if (!renderer) return;
            
            var targetMat = GetMaterial(renderer.GetType());
            
            #if URP
            if (SRPBatcherEnabled())
            {
                //Create a unique material per instance
                material = new Material(targetMat);
                material.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
                material.enableInstancing = false;
                material.name += " (Instance)";
                renderer.material = material;
            }
            else
            {
                //Share a common material and use per-instance properties
                material = targetMat;
                material.enableInstancing = true;
                renderer.sharedMaterial = material;
            }
            #endif
            
            #if DEVELOPMENT_BUILD
            //Debug.LogFormat("[GrassBender {0}] Renderer:{1} - Material:{2} - Shader:{3}", GetInstanceID(), renderer, material ? "True" : "False", material ? material.shader.name : "None");
            #endif
        }
        
        private readonly int paramsID = Shader.PropertyToID("_Params");
        private readonly int _SrcFactor = Shader.PropertyToID("_SrcFactor");
        private readonly int _DstFactor = Shader.PropertyToID("_DstFactor");
        
        /// <summary>
        /// Passes any change in parameters to the material. Must be called for changes to have an effect on rendering.
        /// </summary>
        public void UpdateProperties()
        {
            if (!renderer) GetRenderer();

            if (!material) SetupMaterial();

            #if URP
            if (SRPBatcherEnabled())
            {
                material.SetVector(paramsID, new Vector4(flattenStrength, heightOffset, pushStrength, scaleMultiplier));
                
                material.renderQueue = (alphaBlending ? 3000 : 2000 ) + sortingLayer;
                material.SetFloat(_SrcFactor, alphaBlending ? (int)UnityEngine.Rendering.BlendMode.SrcAlpha : (int)UnityEngine.Rendering.BlendMode.One);
                material.SetFloat(_DstFactor, alphaBlending ? (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha : (int)UnityEngine.Rendering.BlendMode.Zero);
                
                renderer.SetPropertyBlock(null);
            }
            else
            {
                props.SetVector(paramsID, new Vector4(flattenStrength, heightOffset, pushStrength, scaleMultiplier));
                
                //Instanced materials would not support render state changes such as the queue and blend factors...
                
                renderer.SetPropertyBlock(props);
            }
            #endif
        }

        public void GetRenderer()
        {
            renderer = GetComponent<Renderer>();

            Type type = renderer.GetType();

            if (type == typeof(MeshRenderer))
            {
                meshFilter = renderer.gameObject.GetComponent<MeshFilter>();
            }
            else
            {
                meshFilter = null;
            }

            
            if (type == typeof(TrailRenderer))
            {
                trailRenderer = renderer.gameObject.GetComponent<TrailRenderer>();
            }
            else
            {
                trailRenderer = null;
            }
            
            if (type == typeof(LineRenderer))
            {
                lineRenderer = renderer.gameObject.GetComponent<LineRenderer>();
            }
            else
            {
                lineRenderer = null;
            }
        }

        private void OnDisable()
        {
            if (trailRenderer)
            {
                trailRenderer.emitting = false;
            }
        }

        private Vector3 targetPosition;
        private static Vector3 TrailRotation = new Vector3(-90f, 0f, 0f);
        
        private void Update()
        {
            //Trail lifetime only updates when its being moved. If a trail is still for 1 minute, then moves, the entire trail will jolt to match up
            //Slightly jitter its position to force it to constantly/smoothly update
            if (trailRenderer)
            {
                if (forceTrailUpdating)
                {
                    targetPosition = trailRenderer.transform.position;
                    trailRenderer.transform.position = Vector3.Lerp(targetPosition, targetPosition + (Random.insideUnitSphere * 0.0001f), Time.deltaTime * 1000f);
                }

                //Trails always billboard towards the camera.
                //This behaviour is internal and unwanted, it should billboard towards the view/projection it's being rendered with instead
                //Only solution is to force it to a flat orientation
                trailRenderer.alignment = LineAlignment.TransformZ;
                this.transform.rotation = Quaternion.Euler(TrailRotation);
            }
        }
        
        private static Material GetMaterial(Type type)
        {
            return (type == typeof(MeshRenderer) || type == typeof(ParticleSystemRenderer)) ? MeshMaterial : TrailMaterial;
        }

        private const string TRAIL_KEYWORD = "_TRAIL";
        public const string BEND_SHADER_NAME = "Hidden/Nature/Grass Bend Mesh";

        private static Material _TrailMaterial;
        private static Material TrailMaterial
        {
            get
            {
                if (!_TrailMaterial)
                {
                    _TrailMaterial = new Material(Shader.Find(BEND_SHADER_NAME));
                    _TrailMaterial.name = "TrailOrLineBender";
                    _TrailMaterial.EnableKeyword(TRAIL_KEYWORD);
                    _TrailMaterial.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
                }

                return _TrailMaterial;
            }
        }
        
        private static Material _MeshMaterial;
        private static Material MeshMaterial
        {
            get
            {
                if(!_MeshMaterial)
                {
					#if DEVELOPMENT_BUILD
					if(Shader.Find(BEND_SHADER_NAME) == null) Debug.LogError("[Stylized Grass] Could not find the grass bending shader, it was not included in the build.");
					#endif
				
                    _MeshMaterial = new Material(Shader.Find(BEND_SHADER_NAME));
                    _MeshMaterial.name = "MeshOrParticleBender";
                    _MeshMaterial.DisableKeyword(TRAIL_KEYWORD);
                    _MeshMaterial.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
                }

                return _MeshMaterial;
            }
        }
        
        private void OnDrawGizmosSelected()
        {
            if (meshFilter)
            {
                if (renderer.GetType() == typeof(MeshRenderer) && meshFilter.sharedMesh)
                {
                    Gizmos.color = new Color(0f, 0f, 0f, 0.2f);
                    Gizmos.DrawWireMesh(meshFilter.sharedMesh, 0, meshFilter.transform.position + new Vector3(0f, heightOffset, 0f), meshFilter.transform.rotation, meshFilter.transform.lossyScale * scaleMultiplier);
                }
            }
        }
    }
}