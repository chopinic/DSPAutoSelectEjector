using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using BepInEx;
using HarmonyLib;
using System.Threading;


namespace DSPAutoSelectEjector

{
    [BepInPlugin(GUID, NAME, VERSION)]
    [BepInProcess(GAME_PROCESS)]
    public class AutoSelectEjector : BaseUnityPlugin
    {
        public const int TIME_PERIOD = 5000;  //time between each check
        public const string GUID = "cn.yangguang.dsp.autoselectejector";
        public const string NAME = "AutoSelectEjector";
        public const string VERSION = "1.0";
        public const string GAME_PROCESS = "DSPGAME.exe";

        public System.Threading.Timer Mytimer;

        public static PlanetFactory factory;
        public static FactorySystem factorySystem;
        public static DysonSwarm dysonSwarm;
        public static AstroPose[] currentAstroPoses;

        void Start()
        {
            new Harmony(GUID).PatchAll();
            Mytimer = new System.Threading.Timer(new TimerCallback(StateCheck), null, Timeout.Infinite, 1000);
            Mytimer.Change(0, TIME_PERIOD);
        }

        public int getNextOrbit(int orbitId)
        {
            int traverseLimit = 30;
            int traverseCot = 0;
            int nextOrbit = orbitId + 1;
            if (nextOrbit >= dysonSwarm.orbitCursor)
            {
                nextOrbit = 1;
            }
            while (!dysonSwarm.orbits[nextOrbit].enabled)
            {
                traverseCot++;
                if (traverseCot > traverseLimit)
                {
                    return -1;
                }
                nextOrbit++;
                if (nextOrbit >= dysonSwarm.orbitCursor)
                {
                    nextOrbit = 1;
                }
            }
            return nextOrbit;
        }
        public void StateCheck(object state)
        {
            if (GameMain.localPlanet == null)
            {
                return;
            }
            factory = GameMain.localPlanet.factory;
            dysonSwarm = factory.dysonSphere == null ? (DysonSwarm)null : factory.dysonSphere.swarm;
            factorySystem = factory.factorySystem;
            currentAstroPoses = factory.planet.galaxy.astroPoses;
            Logger.LogInfo("Total ejector number: " + factorySystem.ejectorCursor);

            for (int i = 1; i < factorySystem.ejectorCursor; i++)
            {
                EjectorComponent nowEjector = factorySystem.ejectorPool[i];
                if (nowEjector.targetState != EjectorComponent.ETargetState.OK)
                {
                    int nextOrbit = getSuitableOrbit(ref factorySystem.ejectorPool[i]);
                    if (nextOrbit != -1)
                    {
                        factorySystem.ejectorPool[i].SetOrbit(nextOrbit);
                        Logger.LogInfo("Ejector " + i + " set to orbit " + nextOrbit + "\r\n");
                    }
                    else
                    {
                        Logger.LogInfo("Ejector " + i + " can't find target orbit\r\n");
                    }
                }
            }
            int[] afterStates = CountEjectorState();
            Logger.LogInfo("\r\n" + afterStates[0] + " ejectors OK; \r\n" +
                afterStates[1] + " ejectors AngleLimit; \r\n" +
                afterStates[2] + " ejectors Blocked; \r\n" +
                afterStates[3] + " ejectors None; \r\n");
        }

        public int getSuitableOrbit(ref EjectorComponent nowEjector)
        {
            for (int j = 1; j <= dysonSwarm.orbitCursor; j++)
            {
                bool isEjectable = AngleCheck(ref nowEjector, j);
                if (isEjectable)
                {
                    return j;
                }
            }
            return -1;
        }

        bool AngleCheck(ref EjectorComponent t, int orbitId)
        {
            if (orbitId >= dysonSwarm.orbitCursor || orbitId != dysonSwarm.orbits[orbitId].id || !dysonSwarm.orbits[orbitId].enabled)
                return false;
            int index1 = t.planetId / 100 * 100;
            float num3 = (float)((double)t.localAlt + (double)t.pivotY + ((double)t.muzzleY - (double)t.pivotY) / (double)Mathf.Max(0.1f, Mathf.Sqrt((float)(1.0 - (double)t.localDir.y * (double)t.localDir.y))));
            Vector3 v1 = new Vector3(t.localPosN.x * num3, t.localPosN.y * num3, t.localPosN.z * num3);
            VectorLF3 vectorLf3_1 = currentAstroPoses[t.planetId].uPos + Maths.QRotateLF(currentAstroPoses[t.planetId].uRot, (VectorLF3)v1);
            Quaternion q = currentAstroPoses[t.planetId].uRot * t.localRot;
            VectorLF3 uPos = currentAstroPoses[index1].uPos;
            VectorLF3 b = uPos - vectorLf3_1;
            VectorLF3 vectorLf3_2 = uPos + VectorLF3.Cross((VectorLF3)dysonSwarm.orbits[orbitId].up, b).normalized * (double)dysonSwarm.orbits[orbitId].radius;
            VectorLF3 vectorLf3_3 = vectorLf3_2 - vectorLf3_1;
            t.targetDist = vectorLf3_3.magnitude;
            vectorLf3_3.x /= t.targetDist;
            vectorLf3_3.y /= t.targetDist;
            vectorLf3_3.z /= t.targetDist;
            Vector3 v2 = (Vector3)vectorLf3_3;
            Vector3 vector3 = Maths.QInvRotate(q, v2);
            t.localDir.x = (float)((double)t.localDir.x * 0.899999976158142 + (double)vector3.x * 0.100000001490116);
            t.localDir.y = (float)((double)t.localDir.y * 0.899999976158142 + (double)vector3.y * 0.100000001490116);
            t.localDir.z = (float)((double)t.localDir.z * 0.899999976158142 + (double)vector3.z * 0.100000001490116);
            Logger.LogInfo("ejector " + t.id + " y for orbit " + orbitId + " :" + vector3.y);
            if ((double)vector3.y < 0.08715574 || (double)vector3.y > 0.866025388240814)
            {
                return false;
            }

            for (int index2 = index1 + 1; index2 <= t.planetId + 2; ++index2)
            {
                if (index2 != t.planetId)
                {
                    double uRadius = (double)currentAstroPoses[index2].uRadius;
                    if (uRadius > 1.0)
                    {
                        VectorLF3 vectorLf3_4 = currentAstroPoses[index2].uPos - vectorLf3_1;
                        double num4 = vectorLf3_4.x * vectorLf3_4.x + vectorLf3_4.y * vectorLf3_4.y + vectorLf3_4.z * vectorLf3_4.z;
                        double num5 = vectorLf3_4.x * vectorLf3_3.x + vectorLf3_4.y * vectorLf3_3.y + vectorLf3_4.z * vectorLf3_3.z;
                        if (num5 > 0.0)
                        {
                            double num6 = num4 - num5 * num5;
                            double num7 = uRadius + 120.0;
                            double num8 = num7 * num7;
                            if (num6 < num8)
                            {
                                Logger.LogInfo("ejector " + t.id + " for orbit " + orbitId + " blocked");
                                return false;
                            }
                        }
                    }
                }
            }
            return true;
        }

        int[] CountEjectorState()
        {
            int[] states = { 0, 0, 0, 0 };
            for (int i = 1; i < factorySystem.ejectorCursor; i++)
            {
                EjectorComponent nowEjector = factorySystem.ejectorPool[i];
                switch (nowEjector.targetState)
                {
                    case
                        EjectorComponent.ETargetState.AngleLimit:
                        states[1]++;
                        break;
                    case
                        EjectorComponent.ETargetState.Blocked:
                        states[2]++;
                        break;
                    case
                        EjectorComponent.ETargetState.None:
                        states[3]++;
                        break;
                    case
                        EjectorComponent.ETargetState.OK:
                        states[0]++;
                        break;
                }
            }
            return states;
        }

    }
}
