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

    public class getVS (vfpl, i, cfpllat, cfpllong, gs) {


        //initializes the next waypoint
        int j = i++;

        //gets the coordinates of the waypoints
        double origincoordlat = cfpllat[i];
        double goalcoordlat = cfpllat[j];

        double origincoordlong = cfpllong[i];
        double goalcoordlong = cfpllat[j];


        //adds the altitudes
        int goalAlt = vfpl[j];
        int initAlt = vfpl[i];
        int altChange = initAlt - goalAlt;

        //does some f***in complicated coordinate math
        //then the developer remembered there was a class for this
        //and he didn't know how to use it
        //but he will finish it tmrw
        int gsmph = gs * 1.15078;

        var originCoord = new GeoCoordinate(origincoordlat, origincoordlong);
        var goalCoord = new GeoCoordinate(goalcoordlat, goalcoordlong);

        distancem = sCoord.GetDistanceTo(eCoord);
        //distance comes out in meters

        distance = distancem * 3.28084;

        dsscale = distance / gsmph;
        dsscale = dsscale/60;

        vertSpeed = altChange / dsscale;
        return vertSpeed;

    }
}