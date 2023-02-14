using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

public class RoadPlacer : MonoBehaviour
{
    [Header("General")]
    [SerializeField] private GameObject road;
    [Tooltip("This will be the layer the roads are placed in")]
    [SerializeField] private LayerMask roadLayer;
    [Tooltip("Load the textures in this order: \n0 - Solo Road\n1 - Deadend road\n2 - Straight Road\n3 - Three way Road\n4 - Four way road\n5 - Corner road\n\nMake sure that they have the same orientation as the ones provided")]
    [SerializeField] private List<Texture> roadTextures = new List<Texture>();


    [Header("Options")]
    [Tooltip("Uncheck this if you don't want to use the WaypointManager system")]
    [SerializeField] private bool useWaypoints = true;
    [Tooltip("How often does it try to place a road when holding down a button, the lower the number the more times it will check for it.\n A really low number may cause lag")]
    [Range(0.0001f, 1f)]
    [SerializeField] private float tryPlaceRoadCd = 0.05f;


    private bool isPlacing = false;
    private WaypointManager waypointManager;

    private void Start()
    {
        if(roadTextures.Count != 6)
        {
            Debug.LogError("There are not enough textures in the road textures field! Make sure to add them properly");
        }

        if(useWaypoints)
            waypointManager = GetComponent<WaypointManager>();
        StartCoroutine(PlaceRoad());
    }

    private void Update()
    {
        if(Input.GetMouseButtonDown(0))
        {
            PlaceSingleRoad();
        }

        if(Input.GetMouseButton(0))
        {
            MultipleRoads(true);
        }

        if(Input.GetMouseButtonUp(0))
        {
            MultipleRoads(false);
        }

        if(Input.GetMouseButtonDown(1))
        {
            RemoveRoad();
        }
    }

    /// <summary>
    /// Call this function with the Input.GetDown function
    /// </summary>
    public void PlaceSingleRoad()
    {
        TryPlaceRoad();
    }

    /// <summary>
    /// Call this function with the Input.Get() function
    /// </summary>
    /// <param name="_isPlacing"></param>
    public void MultipleRoads(bool _isPlacing)
    {
        this.isPlacing = _isPlacing;
    }

    /// <summary>
    /// Call this function when you want to delete a road and have its neighbours update
    /// </summary>
    public void RemoveRoad()
    {
        StartCoroutine(TryRemoveRoad());
    }

    private IEnumerator TryRemoveRoad()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        Vector3 pos = new Vector3(0, -100, 0);
        if (Physics.Raycast(ray, out hit, Mathf.Infinity, roadLayer))
        {
            pos = hit.collider.gameObject.transform.position;
            if (useWaypoints)
                waypointManager.RemoveWaypoint(hit.collider.gameObject.transform.position);
            Destroy(hit.collider.gameObject);      
        }
        yield return new WaitForSeconds(.01f);
        if(pos.y != -100)
        {
            for (int i = 0; i < 4; i++)
            {
                Vector3 roadCheckerPos = pos;
                switch (i)
                {
                    case 0:
                        roadCheckerPos.x += 1; // Check right
                        break;
                    case 1:
                        roadCheckerPos.x -= 1; // Check left
                        break;
                    case 2:
                        roadCheckerPos.z += 1; // Check Front
                        break;
                    case 3:
                        roadCheckerPos.z -= 1; // Check Back
                        break;
                }
                List<Collider> cols = Physics.OverlapBox(roadCheckerPos, road.transform.localScale / 2.1f, Quaternion.identity, roadLayer).ToList();

                if (cols.Count > 0)
                {
                    CheckNeighbourPlaced(cols[0].gameObject, false, 1);
                }
            }
        }
    }

    private IEnumerator PlaceRoad()
    {
        while(true)
        {
            yield return new WaitForSeconds(tryPlaceRoadCd);
            if(isPlacing)
            {
                TryPlaceRoad();
            }
        }
    }

    private void TryPlaceRoad()
    {

        // Get clicked tile
        Vector3 tile = Vector3.zero;

        Plane plane = new Plane(Vector3.up, Vector3.zero);
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        float rayOut = 0f;

        if (plane.Raycast(ray, out rayOut))
        {
            tile = ray.GetPoint(rayOut) - new Vector3(.5f, 0f, .5f);
            tile = new Vector3(Mathf.CeilToInt(tile.x), 0f, Mathf.CeilToInt(tile.z));
        }

        List<Collider> obstacles = Physics.OverlapBox(tile, road.transform.localScale / 2.1f, Quaternion.identity, roadLayer).ToList();
        if (obstacles.Count > 0)
            return;

        // If tile is free, place road

        GameObject roadAux = Instantiate(road, tile, Quaternion.identity, transform);
        
        CheckNeighbourPlaced(roadAux, true, 0);
    }


    private void CheckNeighbourPlaced(GameObject roadAux, bool isNew, int isRemoving)
    {
        List<GameObject> neighbours = new List<GameObject>();

        List<bool> directions = new List<bool>() {false, false, false, false};

        for (int i = 0; i < 4; i++)
        {
            Vector3 roadCheckerPos = roadAux.transform.position;
            switch (i)
            {
                case 0:
                    roadCheckerPos.x += 1; // Check right
                    break;
                case 1:
                    roadCheckerPos.x -= 1; // Check left
                    break;
                case 2:
                    roadCheckerPos.z += 1; // Check Front
                    break;
                case 3:
                    roadCheckerPos.z -= 1; // Check Back
                    break;
            }
            List<Collider> cols = Physics.OverlapBox(roadCheckerPos, road.transform.localScale / 2.1f, Quaternion.identity, roadLayer).ToList();

            if (cols.Count > 0)
            {
                directions[i] = true;
                neighbours.Add(cols[0].gameObject);
                if(isNew)
                    CheckNeighbourPlaced(cols[0].gameObject, false, 0);
            }
        }

        SetTextureAndRotation(roadAux, directions, neighbours.Count);
       
    }

    private void SetTextureAndRotation(GameObject roadAux, List<bool> directions, int neighbours)
    {
        roadAux.transform.rotation = Quaternion.identity;
        int textureIndex = 0;

        if (neighbours == 4)
        {
            if (useWaypoints)
                waypointManager.AddWaypoint(roadAux.transform.position);
            textureIndex = 4;
        } else if(neighbours == 3)
        {
            if (useWaypoints)
                waypointManager.AddWaypoint(roadAux.transform.position);

            textureIndex = 3;

            if (directions[3] && !directions[2]) // There is one below not one on top
                roadAux.transform.rotation = Quaternion.Euler(new Vector3(0f, 180f, 0f));
            else if(!directions[3] && directions[2]) // There is one on top not one below
                roadAux.transform.rotation = Quaternion.Euler(new Vector3(0f, 0f, 0f));
            else if (directions[0]) // Top and Below and right
                roadAux.transform.rotation = Quaternion.Euler(new Vector3(0f, 90f, 0f));
            else // Top and Below and left
                roadAux.transform.rotation = Quaternion.Euler(new Vector3(0f, -90f, 0f));

        } else if(neighbours == 2)
        {
            if(directions[0] && directions[1]) // horizontal
            {
                if (useWaypoints)
                    waypointManager.RemoveWaypoint(roadAux.transform.position);

                textureIndex = 2;

                roadAux.transform.rotation = Quaternion.Euler(new Vector3(0f, 90f, 0f));

            } else if(directions[2] && directions[3]) // Vertical
            {
                if (useWaypoints)
                    waypointManager.RemoveWaypoint(roadAux.transform.position);

                textureIndex = 2;
            }
            else // We have a turn
            {
                if (useWaypoints)
                    waypointManager.AddWaypoint(roadAux.transform.position);

                textureIndex = 5;

                if (directions[0]) // Right
                {
                    if (directions[2]) // Top
                        roadAux.transform.rotation = Quaternion.Euler(new Vector3(0f, 90f, 0f));
                    else
                        roadAux.transform.rotation = Quaternion.Euler(new Vector3(0f, 180f, 0f));
                } else if (directions[1]) // Left
                {
                    if (directions[2]) // Top
                        roadAux.transform.rotation = Quaternion.Euler(new Vector3(0f, 0f, 0f));
                    else
                        roadAux.transform.rotation = Quaternion.Euler(new Vector3(0f, -90f, 0f));
                }
                
            }
        } else if (neighbours == 1)
        {
            if (useWaypoints)
                waypointManager.AddWaypoint(roadAux.transform.position);

            textureIndex = 1;

            if (directions[1])
                roadAux.transform.rotation = Quaternion.Euler(new Vector3(0f, -90f, 0f));
            else if(directions[0])
                roadAux.transform.rotation = Quaternion.Euler(new Vector3(0f, 90f, 0f));
            else if (directions[3])
            {
                roadAux.transform.rotation = Quaternion.Euler(new Vector3(0f, 180f, 0f));
            }
        } else
        {
            if (useWaypoints)
                waypointManager.RemoveWaypoint(roadAux.transform.position);

            textureIndex = 0;
        }

        ChangeTexture(roadAux, roadTextures[textureIndex]);
    } 

    private void ChangeTexture(GameObject road, Texture newTexture)
    {
        Renderer renderer;

        if(road.TryGetComponent<Renderer>(out renderer))
        {
            renderer.material.mainTexture = newTexture;
        } else
        {
            road.GetComponentInChildren<Renderer>().material.mainTexture = newTexture;
        }
    }
}
