using Fds.IFAPI;
using System;
using System.Device.Location.GeoCoordinate;


namespace FMC
{
    public class startVNAV
    {
        if (public bool IsAutopilotOn { get; set; } == true) {
            VNAV();
        }
    }

    public class waypointConfirm (fpl[], i){


        //imports waypoints from IF
        public APIWaypoint[] Waypoints { get; set; }
        //the following code confirms the name of the waypoint
        if (fpl[i] == APIWaypoint[i]) {
            return true;
        }
    }

    public class getVS (vfpl, i, cfpllat, cfpllong) {


        //initializes the next waypoint
        int j = i++;

        //gets the coordinates of the waypoints
        float origincoordlat = cfpllat[i];
        float goalcoordlat = cfpllat[j];

        float origincoordlong = cfpllong[i];
        float goalcoordlong = cfpllat[j];


        //does some f***in complicated coordinate math
        //then the developer remembered there was a class for this
        //and he didn't know how to use it
        //but he will finish it tmrw


    }
}