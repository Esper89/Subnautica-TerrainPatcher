// 
// Main component you should have attached to a GameObject in your scene.
//
// There should only be one OriginShift component in your scene.
//
// Make sure that you assign the OriginShift.singleton.focus transform field so the script knows where to recenter the world to.
//     (Typically the focus is the player) - you can also assign at runtime if you need like in the example Mirror scripts.
// 

using System;
using System.Collections;
using System.Linq;
using TerrainPatcher;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.VFX;
using UWE;

namespace twoloop
{
    public class OriginShift :  MonoBehaviour
    {
        public static OriginShift singleton;

        public enum OffsetPrecisionMode : byte
        {
            Float,
            Double,
            Decimal,
        }

        /// <summary>
        /// The object that will stay near (0,0,0).
        /// This must be assigned for the world to recenter. Assign this through code or inspector.
        /// </summary>
        public Transform focus;

#if MIRROR_43_0_OR_NEWER
        public NetworkOS networkOS;

        [System.Serializable]
        public struct AbsolutePosition
        {
            public Offset offset;
            public Vector3 position;
            
            public AbsolutePosition(Offset offset, Vector3 position)
            {
                this.offset = offset;
                this.position = position;
            }
            
            public Vector3 ToLocal()
            {
                return RemoteToLocal(offset, position);
            }

            public override string ToString()
            {
                return offset.ToVector3().ToString() + " " + position;
            }
        }
        
        
#endif

        public Offset _localOffset;

        /// <summary>
        /// How much the world has moved to keep the focus Transform position near (0,0,0)
        /// </summary>
        public static Offset LocalOffset
        {
            get
            {
                if (singleton)
                {
                    return singleton._localOffset;
                }
                else
                {
                    Debug.Log(
                        "OriginShift: get LocalOffset was called when there was no OriginShift initialized.");
                    return new Offset();
                }
            }
            private set
            {
                if (singleton)
                {
                    singleton._localOffset = value;

#if MIRROR_43_0_OR_NEWER
                    if (singleton.networkOS && singleton.networkOS.isServer)
                    {
                        singleton.networkOS.SetHostOffset(value);
                    }
#endif
                }
                else
                {
#if MIRROR_43_0_OR_NEWER
                    // Condition for resetting static value when there is no singleton
                    NetworkOS.hostOffset = value;
#endif
                }
            }
        }

        public struct Offset
        {
            public decimal xDecimal;
            public decimal yDecimal;
            public decimal zDecimal;
            
            public double xDouble;
            public double yDouble;
            public double zDouble;
            
            public Vector3 vector;
            
            public Offset(Offset copy)
            {
                vector = new Vector3();
                xDouble = yDouble = zDouble = 0;
                xDecimal = yDecimal = zDecimal = 0;

                switch (singleton.precisionMode)
                {
                    case OffsetPrecisionMode.Float:
                        vector = copy.vector;
                        break;
                    case OffsetPrecisionMode.Double:
                        xDouble = copy.xDouble;
                        yDouble = copy.yDouble;
                        zDouble = copy.zDouble;
                        break;
                    case OffsetPrecisionMode.Decimal:
                        xDecimal = copy.xDecimal;
                        yDecimal = copy.yDecimal;
                        zDecimal = copy.zDecimal;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            public static Offset CreateWithFloat(float x, float y, float z)
            {
                Offset o = new Offset
                {
                    vector = new Vector3(x, y, z)
                };
                return o;
            }
            
            public static Offset CreateWithDouble(double x, double y, double z)
            {
                Offset o = new Offset
                {
                    xDouble = x,
                    yDouble = y,
                    zDouble = z
                };
                return o;
            }
            
            public static Offset CreateWithDecimal(decimal x, decimal y, decimal z)
            {
                Offset o = new Offset
                {
                    xDecimal = x,
                    yDecimal = y,
                    zDecimal = z
                };
                return o;
            }

            public static Offset Subtract(Offset from, Offset other)
            {
                switch (singleton.precisionMode)
                {
                    case OffsetPrecisionMode.Float:
                        from.vector -= other.vector;
                        break;
                    case OffsetPrecisionMode.Double:
                        from.xDouble -= other.xDouble;
                        from.yDouble -= other.yDouble;
                        from.zDouble -= other.zDouble;
                        break;
                    case OffsetPrecisionMode.Decimal:
                        from.xDecimal -= other.xDecimal;
                        from.yDecimal -= other.yDecimal;
                        from.zDecimal -= other.zDecimal;
                        break;
                }

                return from;
            }
            public static Offset Add(Offset o, Vector3 other)
            {
                switch (singleton.precisionMode)
                {
                    case OffsetPrecisionMode.Float:
                        o.vector += other;
                        break;
                    case OffsetPrecisionMode.Double:
                        o.xDouble += other.x;
                        o.yDouble += other.y;
                        o.zDouble += other.z;
                        break;
                    case OffsetPrecisionMode.Decimal:
                        o.xDecimal += (decimal)other.x;
                        o.yDecimal += (decimal)other.y;
                        o.zDecimal += (decimal)other.z;
                        break;
                }

                return o;
            }
            public Vector3 ToVector3()
            {
                switch (singleton.precisionMode)
                {
                    case OffsetPrecisionMode.Double:
                        return new Vector3((float) xDouble, (float) yDouble, (float) zDouble);
                    case OffsetPrecisionMode.Float:
                        return this.vector;
                    case OffsetPrecisionMode.Decimal:
                        return new Vector3((float) xDecimal, (float) yDecimal,
                            (float) zDecimal);
                    default:
                        return Vector3.zero;
                }
            }
            public new string ToString()
            {
                switch (singleton.precisionMode)
                {
                    case OffsetPrecisionMode.Double:
                        return "Precision: " + singleton.precisionMode + " (" + xDouble.ToString("N3") + ", " + yDouble.ToString("N3") +
                               ", " +
                               zDouble.ToString("N3") + ")";
                    case OffsetPrecisionMode.Float:
                        return "Precision: " + singleton.precisionMode + " " + vector.ToString("N3");
                    case OffsetPrecisionMode.Decimal:
                        return "Precision: " + singleton.precisionMode + " (" + xDecimal.ToString("N3") + ", " +
                               yDecimal.ToString("N3") + ", " +
                               zDecimal.ToString("N3") + ")";
                    default:
                        return string.Empty;
                }
            }
        }

        public Vector3 playerbeforeShiftAmount;
        public Vector3 playerbeforeShiftVelocity;
        /// <summary>
        /// How precise should client offsets be?
        ///
        /// For single-player, set to Float.
        ///
        /// Float - 7 digits safe
        /// Double - 15 digits safe
        /// Decimal - 28 digits safe
        ///
        /// </summary>
        [Header("Settings")]
        [Tooltip("How precise should client offsets be? Single player? -> set this to Float. Don't change at runtime.")]
        public OffsetPrecisionMode precisionMode = OffsetPrecisionMode.Float;

        /// <summary>
        /// Should the world recenter on update?
        ///
        /// Set true to support extremely high speeds.
        ///
        /// If true, the distance threshold and tick delay are ignored.
        ///
        /// ***You can change this at runtime***
        /// 
        /// </summary>
        [Tooltip("Should the world recenter every frame? Use this to support extremely high speeds. Can be changed at runtime")]
        public bool isContinuous = false;

        /// <summary>
        /// How far the player must move to trigger the world to recenter.
        /// Lower this value if you still experience vertex jitter on your models.
        /// </summary>

        [Tooltip(
            "How far the player must move to trigger the world to recenter. Lower this value if you still experience vertex jitter on your models.")]
        public float distanceThreshold = 500f;

        /// <summary>
        /// The duration of time between checking whether the world should recenter (inverse of rate).
        /// If your focus Transform moves very fast you may want to raise this value depending on how quickly it surpasses the distance threshold.
        /// </summary>
        [Tooltip("The duration of time between checking whether the world should recenter (inverse of rate). Optimal = distanceThreshold / (maxSpeed in meters per second))")]
        public float tickDelay = 10f;

        /// <summary>
        /// Whether or not vertical (Y-Axis) displacement triggers the world to recenter
        /// </summary>
        [Tooltip("Whether or not vertical (Y-Axis) displacement triggers the world to recenter")]
        public bool useVerticalRecentering = false;
        
        /// <summary>
        /// Invoked with new world offset and translation
        /// </summary>
        [System.Serializable]
        public class OriginShiftEvent : UnityEvent<Vector3, Vector3>
        {
        }
        
        /// <summary>
        /// Invoked with new world offset and translation
        /// </summary>
        [Tooltip("Invoked with (Vector3 newWorldOffset, Vector3 translation")]
        public OriginShiftEvent onOriginShifted = new OriginShiftEvent();

        /// <summary>
        /// Invoked with new world offset and translation
        /// </summary>
        public static OriginShiftEvent OnOriginShifted = new OriginShiftEvent();
        
        /// <summary>
        /// Some physics controllers require that you set this false..
        /// Notably: RealisticCarController
        /// This is currently a workaround until we find a better solution
        /// </summary>
        [Header("Experimental")]
        [Tooltip("You may need to set this to false if you use certain third party physics controllers.")]
        public bool disableCollidersOnRecenter = true;
        
        public enum RecenterTiming
        {
            EndOfFrame,
            LateUpdate
        }
        
        /// <summary>
        /// Determines the part of the frame when the entire world recenters.
        /// Your desired setting may change depending on how/when you move your camera.
        /// </summary>
        [Tooltip("Your desired setting may change depending on how/when you move your camera.")]
        public RecenterTiming recenterTiming = RecenterTiming.EndOfFrame;

        private bool _needsRecenter;

        private readonly YieldInstruction _waitForEndOfFrame = new WaitForEndOfFrame();
        private YieldInstruction _waitForTick;

        private Vector3 _focusPosition;
        
        private static ParticleSystem.Particle[] particlesBuffer;

        /// <summary>
        /// The main method used for recentering the client's world.
        /// Feel free to call this and not use the coroutine if you want to manually override it.
        /// </summary>
        public static void Recenter()
        {
            singleton._needsRecenter = true;
        }
        /// <summary>
        /// Transforms a position from a remote client's world space to the local client's world space.
        /// You should use this whenever you want to communicate a position from a remote client.
        /// </summary>
        /// <param name="remoteOffset">The remote client's OriginShift.LocalOffset</param>
        /// <param name="remotePosition">The transform.position of the object on the remote client</param>
        /// <returns>The position adjusted to the local client's world offset</returns>
        public static Vector3 RemoteToLocal(Offset remoteOffset, Vector3 remotePosition)
        {
            var o = new Offset(remoteOffset);

            o = Offset.Subtract(o, LocalOffset);

            return remotePosition + o.ToVector3();
        }

        private void OnValidate()
        {
#if MIRROR_43_0_OR_NEWER
            if (networkOS == null)
            {
                networkOS = FindObjectOfType<NetworkOS>();
            }
#endif
        }

        private void Awake()
        {
            if(singleton == null)
            {
                singleton = this;
            }
            else
            {
                Debug.LogWarning("There can only be one OriginShift in the scene.");
                Destroy(this);
            }
            
#if MIRROR_43_0_OR_NEWER
            if (networkOS == null)
            {
                networkOS = FindObjectOfType<NetworkOS>();
            }
#endif
        }
        
        public void Start()
        {
            _waitForTick = new WaitForSeconds(tickDelay);

            StartCoroutine(CheckDistanceAndRecenterRoutine());
        }

        public void LateUpdate()
        {
            if (isContinuous && focus)
            {
                DoRecenter(focus.position);
            }
            else
            {
                // Recentering is called from Late Update so the camera does show the world moving for that frame
                if (_needsRecenter)
                {
                    switch (recenterTiming)
                    {
                        case RecenterTiming.LateUpdate:
                            DoRecenter(focus.position);
                            break;
                        case RecenterTiming.EndOfFrame:
                            StartCoroutine(DoRecenterEndOfFrame());
                            break;
                    }
                    
                    _needsRecenter = false;
                }
            }
        }

        public void OnDisable()
        {
            ResetOrigin();
        }

        public static void ResetOrigin()
        {
            if (singleton)
            {
                singleton.DoRecenter(LocalOffset.ToVector3());
            }

            LocalOffset = new Offset();
            
#if MIRROR_43_0_OR_NEWER
            // Condition for resetting static value when there is no singleton
            NetworkOS.hostOffset = new Offset();
#endif
        }

        private IEnumerator CheckDistanceAndRecenterRoutine()
        {
            for (;;)
            {
                yield return _waitForTick;

                // Recentering happens every frame in lateupdate for continuous mode
                if (isContinuous)
                {
                    continue;
                }

                RecenterIfNeeded();
            }
            // ReSharper disable once IteratorNeverReturns
        }

        /// <summary>
        /// Recenters the world if the distance threshold has been passed
        /// </summary>
        public void RecenterIfNeeded()
        {
            if (!focus)
            {
                return;
            }
            
            _focusPosition = focus.position;

            if (!useVerticalRecentering)
            {
                _focusPosition.y = 0;
            }

            if (_focusPosition.magnitude > distanceThreshold)
            {
                Recenter();
            }
        }

        /// <summary>
        /// The actual implementation for recentering the client world.
        /// </summary>
        private void DoRecenter(Vector3 focusPosition)
        {
            _focusPosition = focusPosition;
            var velocity = Player.main.gameObject.GetComponent<Rigidbody>().velocity;
            if (!useVerticalRecentering)
            {
                _focusPosition.y = 0;
            }

            LocalOffset = Offset.Add(LocalOffset, _focusPosition);

            // Temporarily disable interpolation so everything can recenter instantly
            var rbs = FindObjectsOfType<Rigidbody>();
            var rbInterpolationModes = new RigidbodyInterpolation[rbs.Length];
            var colliders = FindObjectsOfType<Collider>();
            bool[] collidersWasEnabled = { };

            if (disableCollidersOnRecenter)
            {
                collidersWasEnabled = new bool[colliders.Length];

                // Disable all colliders
                for (int i = 0; i < colliders.Length; i++)
                {
                    collidersWasEnabled[i] = colliders[i].enabled;
                    colliders[i].enabled = false;
                }
            }

            for (int i = 0; i < rbs.Length; i++)
            {
                // Store interpolation modes then disable interpolation
                rbInterpolationModes[i] = rbs[i].interpolation;
                rbs[i].interpolation = RigidbodyInterpolation.None;
            }

            var agents = FindObjectsOfType<UnityEngine.AI.NavMeshAgent>();

            // Disable all Nav Mesh Agents so it doesn't throlayer.main.rigidBody.Sleep();w an error when they are off the navmesh temporarily.
            foreach (var agent in agents)
            {
                agent.enabled = false;
            }

            // Move all root GameObjects
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var rootGameObjects = SceneManager.GetSceneAt(i).GetRootGameObjects();
                foreach (var item in rootGameObjects)
                {
                    if (item.name == "MainCamera (UI)" || item.GetComponent<uGUI_BuilderMenu>() || item.name == "MainCamera")
                    {
                        continue;
                    }

                    if (item.GetComponent<SignalPing>())
                    {
                        item.GetComponent<SignalPing>().pos -= _focusPosition;
                    }
                    item.transform.position -= _focusPosition;
                }
            }

            // Re-enable nav mesh agents 
            foreach (var agent in agents)
            {
                agent.enabled = true;
            }

            // Revert interpolation to original
            for (var i = 0; i < rbs.Length; i++)
            {
                rbs[i].interpolation = rbInterpolationModes[i];
            }

            if (disableCollidersOnRecenter)
            {
                // Re-enable colliders
                for (int i = 0; i < colliders.Length; i++)
                {
                    colliders[i].enabled = collidersWasEnabled[i];
                }
            }
            
            MoveParticles(-_focusPosition);
            MoveLineRenderers(-_focusPosition);
            MoveTrailRenderers(-_focusPosition);
            Player.main.transform.position = focusPosition - _focusPosition;
            Player.main.GetComponent<Rigidbody>().velocity = velocity;
            LargeWorldStreamer.main.ReloadSettings();
            Physics.SyncTransforms();
            // Invoke instance event (for inspector callbacks)
            onOriginShifted.Invoke(LocalOffset.ToVector3(), -_focusPosition);
            
            // Invoke static event
            OnOriginShifted.Invoke(LocalOffset.ToVector3(), -_focusPosition);
        }

        /// <summary>
        /// Starting this coroutine will recenter the world at the end of frame.
        /// Recentering is preformed at end of frame so there is no visible recentering of objects.
        /// </summary>
        private IEnumerator DoRecenterEndOfFrame()
        {
            yield return _waitForEndOfFrame;

            DoRecenter(focus.position);
        }

        private readonly int worldOffsetVFXGraph = Shader.PropertyToID("WorldOffset_position");
        private void MoveParticles(Vector3 offset)
        {
            // Shift legacy particles
            var particleSystems = FindObjectsOfType<ParticleSystem>();
            foreach (var system in particleSystems)
            {
                if (system.main.simulationSpace != ParticleSystemSimulationSpace.World)
                    continue;
 
                int particlesNeeded = system.main.maxParticles;
 
                if (particlesNeeded <= 0)
                    continue;
 
                bool wasPaused = system.isPaused;
                bool wasPlaying = system.isPlaying;
 
                if (!wasPaused)
                    system.Pause();
 
                // ensure a sufficiently large array in which to store the particles
                if (particlesBuffer == null || particlesBuffer.Length < particlesNeeded)
                {
                    particlesBuffer = new ParticleSystem.Particle[particlesNeeded];
                }
 
                // now get the particles
                int num = system.GetParticles(particlesBuffer);
 
                for (int i = 0; i < num; i++)
                {
                    particlesBuffer[i].position += offset;
                }
 
                system.SetParticles(particlesBuffer, num);
 
                if (wasPlaying)
                {
                    system.Play();
                }
            }

            // Shift VFX graph simulation space for world space particles
            var visualEffects = FindObjectsOfType<VisualEffect>();
            foreach (var visualEffect in visualEffects)
            {
                if (!visualEffect.HasVector3(worldOffsetVFXGraph))
                {
                    continue;
                }
                visualEffect.SetVector3(worldOffsetVFXGraph, -LocalOffset.ToVector3());
            }
        }

        private static void MoveTrailRenderers(Vector3 offset)
        {
            var trailRenderers = FindObjectsOfType<TrailRenderer>();
            
            foreach (var trailRenderer in trailRenderers)
            {
                // Store
                bool wasEmitting = trailRenderer.emitting;
                bool wasEnabled = trailRenderer.enabled;
                
                var newPositions = new Vector3[trailRenderer.positionCount];
                var trailRendererPositionsCount = trailRenderer.GetPositions(newPositions);

                
                // Weird problems can happen if you don't disable the TrailRenderer before moving positions. This part is a work in progress...
                // TrailRenderer is invisible if you disable this on continuous
                if (!singleton.isContinuous)
                {
                    trailRenderer.enabled = false;
                    trailRenderer.emitting = false;
                }
                
                for (int i = 0; i < trailRendererPositionsCount; i++)
                {
                    newPositions[i] += offset;
                }
                trailRenderer.SetPositions(newPositions);

                // Restore
                trailRenderer.enabled = wasEnabled;
                trailRenderer.emitting = wasEmitting;
            }
        }
 
        private static void MoveLineRenderers(Vector3 offset)
        {
            var lines = FindObjectsOfType<LineRenderer>();
            foreach (var line in lines)
            {
                if (!line.useWorldSpace)
                {
                    continue;
                }
                
                var positions = new Vector3[line.positionCount];
 
                int positionCount = line.GetPositions(positions);
                for (int i = 0; i < positionCount; i++)
                {
                    positions[i] += offset;
                }
                
                line.SetPositions(positions);
            }
        }
    }
}