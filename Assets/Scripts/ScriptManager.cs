using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ScriptManager : MonoBehaviour
{
    public NetworkServer ns;
    public AgentManager am;
    public MapManager mm;
    public TaskManager tm;

    void Start()
    {
        mm.PublicStart();
        am.PublicStart();
        ns.PublicStart();
        tm.PublicStart();
    }
}
