// copyright Runette Software Ltd, 2020. All rights reserved
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;
using GeoJSON.Net.Geometry;
using GeoJSON.Net.Feature;
using GeoJSON.Net;
using System.Threading.Tasks;
using Project;
using Newtonsoft.Json.Linq;
using UnityEngine.UI;

namespace Virgis
{

    /// <summary>
    /// Controls an instance of a Polygon Layer
    /// </summary>
    public class PolygonLayer : Layer<GeographyCollection, FeatureCollection>
    {

        // The prefab for the data points to be instantiated
        public GameObject CylinderLinePrefab; // Prefab to be used for cylindrical lines
        public GameObject CuboidLinePrefab; // prefab to be used for cuboid lines
        public GameObject SpherePrefab; // prefab to be used for Vertex handles
        public GameObject CubePrefab; // prefab to be used for Vertex handles
        public GameObject CylinderPrefab; // prefab to be used for Vertex handle
        public GameObject PolygonPrefab; // Prefab to be used for the polygons
        public GameObject LabelPrefab; // Prefab to used for the Labels
        public Material Mat; // Material to be used for the Polygon

        private GameObject HandlePrefab;
        private GameObject LinePrefab;

        private GeoJsonReader geoJsonReader;


        public override async Task _init(GeographyCollection layer)
        {
            geoJsonReader = new GeoJsonReader();
            await geoJsonReader.Load(layer.Source);
            features = geoJsonReader.getFeatureCollection();
        }

        public override void _add(MoveArgs args)
        {
            throw new System.NotImplementedException();
        }

        public override void _draw()
        {
            Dictionary<string, Unit> symbology = layer.Properties.Units;

            if (symbology.ContainsKey("point") && symbology["point"].ContainsKey("Shape"))
            {
                Shapes shape = symbology["point"].Shape;
                switch (shape)
                {
                    case Shapes.Spheroid:
                        HandlePrefab = SpherePrefab;
                        break;
                    case Shapes.Cuboid:
                        HandlePrefab = CubePrefab;
                        break;
                    case Shapes.Cylinder:
                        HandlePrefab = CylinderPrefab;
                        break;
                    default:
                        HandlePrefab = SpherePrefab;
                        break;
                }
            }
            else
            {
                HandlePrefab = SpherePrefab;
            }

            if (symbology.ContainsKey("line") && symbology["line"].ContainsKey("Shape"))
            {
                Shapes shape = symbology["line"].Shape;
                switch (shape)
                {
                    case Shapes.Cuboid:
                        LinePrefab = CuboidLinePrefab;
                        break;
                    case Shapes.Cylinder:
                        LinePrefab = CylinderLinePrefab;
                        break;
                    default:
                        LinePrefab = CylinderLinePrefab;
                        break;
                }
            }
            else
            {
                LinePrefab = CylinderLinePrefab;
            }


            foreach (Feature feature in features.Features)
            {
                IDictionary<string, object> properties = feature.Properties;
                string gisId = feature.Id;


                // Get the geometry
                MultiPolygon mPols = null;
                if (feature.Geometry.Type == GeoJSONObjectType.Polygon)
                {
                    mPols = new MultiPolygon(new List<Polygon>() { feature.Geometry as Polygon });
                }
                else if (feature.Geometry.Type == GeoJSONObjectType.MultiPolygon)
                {
                    mPols = feature.Geometry as MultiPolygon;
                }

                foreach (Polygon mPol in mPols.Coordinates)
                {
                    ReadOnlyCollection<LineString> LinearRings = mPol.Coordinates;
                    LineString perimeter = LinearRings[0];
                    Vector3[] poly = Tools.LS2Vect(perimeter);
                    Vector3 center = Vector3.zero;
                    if (properties.ContainsKey("polyhedral") && properties["polyhedral"] != null)
                    {
                        JObject jobject = (JObject)properties["polyhedral"];
                        Point centerPoint = jobject.ToObject<Point>();
                        center = centerPoint.Coordinates.Vector3();
                        properties["polyhedral"] = new Point(Tools.Vect2Ipos(center));
                    }
                    else
                    {
                        center = Datapolygon.FindCenter(poly);
                        properties["polyhedral"] = new Point(Tools.Vect2Ipos(center));
                    }

                    //Create the GameObjects
                    GameObject dataLine = Instantiate(LinePrefab, center, Quaternion.identity);
                    GameObject dataPoly = Instantiate(PolygonPrefab, center, Quaternion.identity);
                    GameObject centroid = Instantiate(HandlePrefab, center, Quaternion.identity);
                    dataPoly.transform.parent = gameObject.transform;
                    dataLine.transform.parent = dataPoly.transform;
                    centroid.transform.parent = dataLine.transform;

                    // add the gis data from geoJSON
                    Datapolygon com = dataPoly.GetComponent<Datapolygon>();
                    com.gisId = gisId;
                    com.gisProperties = properties;
                    com.centroid = centroid.GetComponent<Datapoint>();

                    //Set the label
                    GameObject labelObject = Instantiate(LabelPrefab, center, Quaternion.identity);
                    labelObject.transform.parent = centroid.transform;
                    labelObject.transform.Translate(Vector3.up * symbology["point"].Transform.Scale.magnitude);
                    Text labelText = labelObject.GetComponentInChildren<Text>();

                    if (symbology["body"].ContainsKey("Label") && properties.ContainsKey(symbology["body"].Label))
                    {
                        labelText.text = (string)properties[symbology["body"].Label];
                    }

                    //Draw the Polygon
                    Mat.SetColor("_BaseColor", symbology["body"].Color);
                    com.Draw(perimeter, Mat);
                    dataLine.GetComponent<Dataline>().Draw(perimeter, symbology, LinePrefab, HandlePrefab, null);
                    centroid.SendMessage("SetColor", (Color)symbology["point"].Color);
                    centroid.SendMessage("SetId", -1);
                    centroid.transform.localScale = symbology["point"].Transform.Scale;
                    centroid.transform.localRotation = symbology["point"].Transform.Rotate;
                    centroid.transform.localPosition = symbology["point"].Transform.Position;
                }
            };
        }

        public override void ExitEditsession()
        {
            BroadcastMessage("EditEnd", SendMessageOptions.DontRequireReceiver);
        }

        public override void _cp() { }
        public override void _save()
        {
            Datapolygon[] dataFeatures = gameObject.GetComponentsInChildren<Datapolygon>();
            List<Feature> features = new List<Feature>();
            foreach (Datapolygon dataFeature in dataFeatures)
            {
                Dataline perimeter = dataFeature.GetComponentInChildren<Dataline>();
                Vector3[] vertices = perimeter.GetVerteces();
                List<Position> positions = new List<Position>();
                foreach (Vector3 vertex in vertices)
                {
                    positions.Add(Tools.Vect2Ipos(vertex) as Position);
                }
                LineString line = new LineString(positions);
                if (!line.IsLinearRing())
                {
                    throw new System.ArgumentException("This Polygon is not a Linear Ring", dataFeature.gisProperties.ToString());
                }
                List<LineString> LinearRings = new List<LineString>();
                LinearRings.Add(line);
                IDictionary<string, object> properties = dataFeature.gisProperties;
                Datapoint centroid = dataFeature.centroid;
                properties["polyhedral"] = new Point(Tools.Vect2Ipos(centroid.transform.position));
                features.Add(new Feature(new Polygon(LinearRings), properties, dataFeature.gisId));
            };
            FeatureCollection FC = new FeatureCollection(features);
            geoJsonReader.SetFeatureCollection(FC);
            geoJsonReader.Save();
        }

        public override void Translate(MoveArgs args)
        {
            changed = true;
        }

        public override void MoveAxis(MoveArgs args)
        {
            changed = true;
        }

        /*public override VirgisComponent GetClosest(Vector3 coords)
        {
            throw new System.NotImplementedException();
        }*/

        public override VirgisComponent GetFeature(string id)
        {
            throw new System.NotImplementedException();
        }
    }
}
