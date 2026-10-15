using System;
using System.Linq;
using Qingxi;
using UnityEngine;

namespace Qingxi.Editor
{
    public static class QingxiWaterChecks
    {
        public static void Validate(TownExplorer world)
        {
            var water = world.GetComponentsInChildren<WaterArea>();
            var bridges = world.GetComponentsInChildren<WalkableBridge>();
            Require(water.Count(w => w.IsTerrain) == 1, "Missing terrain water collision");
            Require(water.All(w => !w.Barrier.isTrigger && w.Barrier.enabled && w.Barrier.pathCount > 0), "Water is not solid");
            Require(bridges.Length == 1, "Missing bridge/pier");
            Require(bridges.All(b => b.GetComponent<MeshFilter>().sharedMesh != null &&
                b.GetComponent<MeshRenderer>().sharedMaterial != null), "Missing saved bridge artwork");
            Vector2[] approaches = bridges.SelectMany(b => new[] { b.ApproachA, b.ApproachB }).ToArray();
            Require(QingxiSceneChecks.ReachablePoints(world, approaches).All(r => r), "Unreachable bridge/pier approach");

            foreach (var area in water)
            {
                Bounds bounds = area.Source.bounds;
                int checkedPoints = 0;
                for (float y = bounds.min.y + 0.1f; y < bounds.max.y; y += 0.17f)
                for (float x = bounds.min.x + 0.1f; x < bounds.max.x; x += 0.17f)
                {
                    Vector2 p = new Vector2(x, y);
                    if (bridges.Any(b => b.ContainsDeck(p))) continue;
                    // Exclude the sub-cell antialiasing fringe while independently sampling source art.
                    Vector2[] offsets = { Vector2.zero, Vector2.up * 0.09f, Vector2.down * 0.09f,
                        Vector2.left * 0.09f, Vector2.right * 0.09f };
                    if (!offsets.All(o => QingxiWaterBuilder.IsPaintedWater(area.Source, p + o))) continue;
                    Require(Physics2D.OverlapCircleAll(p, 0.32f).Any(c => !c.isTrigger && c.gameObject != world.Player.gameObject),
                        "Player can stand on painted water at " + p + " in " + world.name +
                        ", source " + area.Source.sprite.name + ", contacts " + string.Join(",", Physics2D.OverlapPointAll(p).Select(c => c.name)));
                    checkedPoints++;
                }
                Require(checkedPoints > 0, "Water sampling did not cover " + area.name);
            }
            CheckPhysics(world, water.Single(w => w.IsTerrain), bridges);
            Debug.Log("WATER CHECK: " + world.name + " / painted water blocked / 5 entrances reachable / bridge railings and real Rigidbody2D crossing passed.");
        }

        static void CheckPhysics(TownExplorer world, WaterArea terrain, WalkableBridge[] bridges)
        {
            var body = world.Player.GetComponent<Rigidbody2D>();
            Vector2 savedPosition = body.position, savedVelocity = body.linearVelocity;
            var savedMode = Physics2D.simulationMode;
            try
            {
                Physics2D.simulationMode = SimulationMode2D.Script;
                bool pushed = false;
                for (float y = -5; y <= 5 && !pushed; y += 1.1f)
                for (float x = -9; x <= 9 && !pushed; x += 0.3f)
                foreach (float sign in new[] { -1f, 1f })
                {
                    Vector2 wet = new Vector2(x, y), direction = Vector2.right * sign;
                    Vector2 start = wet - direction * 1.2f;
                    if (!terrain.Barrier.OverlapPoint(wet) || bridges.Any(b => b.ContainsDeck(wet) || b.ContainsDeck(start)) ||
                        !QingxiSceneChecks.IsFree(world, start) || QingxiWaterBuilder.IsPaintedWater(terrain.Source, start)) continue;
                    var firstHit = Physics2D.CircleCastAll(start, 0.32f, direction, 1.2f)
                        .Where(hit => hit.collider != null && !hit.collider.isTrigger && hit.collider.gameObject != world.Player.gameObject)
                        .OrderBy(hit => hit.distance).FirstOrDefault();
                    if (firstHit.collider != terrain.Barrier) continue;
                    body.position = start;
                    Physics2D.SyncTransforms();
                    bool touchedWater = false;
                    var contacts = new ContactPoint2D[32];
                    for (int i = 0; i < 80; i++)
                    {
                        body.linearVelocity = direction * 4.6f;
                        Physics2D.Simulate(0.02f);
                        Require(!QingxiWaterBuilder.IsPaintedWater(terrain.Source, body.position) || bridges.Any(b => b.ContainsDeck(body.position)),
                            "Player entered open water during shore movement");
                        int count = body.GetContacts(contacts);
                        for (int j = 0; j < count; j++)
                            if (contacts[j].collider == terrain.Barrier || contacts[j].otherCollider == terrain.Barrier) touchedWater = true;
                    }
                    Require(touchedWater, "Shore movement did not physically contact a water barrier");
                    pushed = true;
                    break;
                }
                Require(pushed, "No shore collision exercised");
                foreach (var bridge in bridges)
                {
                    Cross(bridge.ApproachA, bridge.ApproachB);
                    Cross(bridge.ApproachB, bridge.ApproachA);
                    // Solid rails must prevent stepping sideways from the middle of a bridge.
                    body.position = bridge.transform.position;
                    Physics2D.SyncTransforms();
                    Vector2 sideways = bridge.transform.up;
                    for (int i = 0; i < 70; i++) { body.linearVelocity = sideways * 4.6f; Physics2D.Simulate(0.02f); }
                    Require(bridge.ContainsDeck(body.position), "Player walked through a bridge railing");
                }

                void Cross(Vector2 from, Vector2 to)
                {
                    body.position = from;
                    Physics2D.SyncTransforms();
                    for (int i = 0; i < 220 && Vector2.Distance(body.position, to) > 0.04f; i++)
                    {
                        Vector2 remaining = to - body.position;
                        body.linearVelocity = Vector2.ClampMagnitude(remaining / 0.02f, 4.6f);
                        Physics2D.Simulate(0.02f);
                        Require(!QingxiWaterBuilder.IsPaintedWater(terrain.Source, body.position) || bridges.Any(b => b.ContainsDeck(body.position)),
                            "Bridge crossing stepped into open water");
                    }
                    Require(Vector2.Distance(body.position, to) < 0.08f, "Bridge blocks physical crossing: " + from + " to " + to + ", stopped at " + body.position);
                }
            }
            finally
            {
                body.position = savedPosition;
                body.linearVelocity = savedVelocity;
                Physics2D.SyncTransforms();
                Physics2D.simulationMode = savedMode;
            }
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
