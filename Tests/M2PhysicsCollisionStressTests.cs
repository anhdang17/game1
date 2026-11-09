using System;
using System.Collections.Generic;
using System.Numerics;
using GameProject.Core.Movement;
using GameProject.Core.Spatial;
using Xunit;

namespace GameProject.Tests;

/// <summary>
/// Adversarial stress tests for Milestone 2 Physics, Kinematics, Collision, Y-Sorting, and Camera Tracking.
/// Evaluates tunneling, rapid oscillations, concave corners, acute chokepoints, collinear Y-sorting, and camera boundaries.
/// </summary>
public class M2PhysicsCollisionStressTests
{
    // =========================================================================
    // 1. KINEMATICS & RAPID OSCILLATION STRESS TESTS
    // =========================================================================

    [Fact]
    public void Kinematics_RapidOpposingOscillations_VelocityRemainsBoundedAndFinite()
    {
        var world = new CollisionWorld();
        var kin = new CharacterKinematics
        {
            Position = new Vector2(500f, 500f),
            MaxSpeed = 200f,
            Acceleration = 1200f,
            Friction = 1500f
        };

        // Rapidly alternate between Left (-1, 0) and Right (+1, 0) every tick for 2000 ticks
        Vector2[] directions = { new(-1f, 0f), new(1f, 0f) };
        for (int i = 0; i < 2000; i++)
        {
            Vector2 input = directions[i % 2];
            kin.Update(input, world, 0.016f);

            // Assertions on every single tick
            Assert.False(float.IsNaN(kin.Velocity.X), $"Velocity.X was NaN at tick {i}");
            Assert.False(float.IsNaN(kin.Velocity.Y), $"Velocity.Y was NaN at tick {i}");
            Assert.False(float.IsInfinity(kin.Velocity.X), $"Velocity.X was Infinity at tick {i}");
            Assert.True(kin.Velocity.Length() <= 200.01f, $"Velocity exceeded max speed! Length: {kin.Velocity.Length()}");
            Assert.True(kin.WalkCycleTimer >= 0f && kin.WalkCycleTimer < 1.0f, $"Walk cycle timer out of bounds: {kin.WalkCycleTimer}");
            Assert.True(MathF.Abs(kin.GetVerticalBobbingOffset()) <= 2.501f, $"Bobbing offset exceeded: {kin.GetVerticalBobbingOffset()}");
        }
    }

    [Fact]
    public void Kinematics_DiagonalOscillation_NoVelocitySpikes()
    {
        var world = new CollisionWorld();
        var kin = new CharacterKinematics { Position = new Vector2(500f, 500f) };

        Vector2[] diagonals = { new(1f, 1f), new(-1f, -1f), new(1f, -1f), new(-1f, 1f) };
        for (int i = 0; i < 1000; i++)
        {
            Vector2 input = diagonals[i % 4];
            kin.Update(input, world, 0.016f);

            Assert.True(kin.Velocity.Length() <= 200.01f, $"Diagonal speed exceeded max: {kin.Velocity.Length()}");
            Assert.False(float.IsNaN(kin.Position.X));
            Assert.False(float.IsNaN(kin.Position.Y));
        }
    }

    [Fact]
    public void Kinematics_ExtremeDeltaTime_HandledGracefully()
    {
        var world = new CollisionWorld();
        var kin = new CharacterKinematics { Position = new Vector2(200f, 200f) };

        // Zero delta time should not move or mutate
        Vector2 posBefore = kin.Position;
        kin.Update(new Vector2(1f, 0f), world, 0f);
        Assert.Equal(posBefore, kin.Position);

        // Negative delta time should not crash or run backwards
        kin.Update(new Vector2(1f, 0f), world, -0.05f);
        Assert.Equal(posBefore, kin.Position);

        // Huge delta time (e.g. 5.0 seconds pause)
        kin.Update(new Vector2(1f, 0f), world, 5.0f);
        Assert.False(float.IsNaN(kin.Position.X));
        Assert.False(float.IsInfinity(kin.Position.X));
        Assert.True(kin.Velocity.Length() <= 200.01f);
    }

    [Fact]
    public void Kinematics_UnnormalizedLargeInputs_NormalizedGracefully()
    {
        var world = new CollisionWorld();
        var kin = new CharacterKinematics { Position = new Vector2(200f, 200f) };

        // Huge magnitude input (e.g. from raw mouse/joystick without normalization)
        for (int i = 0; i < 50; i++)
        {
            kin.Update(new Vector2(999999f, 999999f), world, 0.016f);
        }

        Assert.True(kin.Velocity.Length() <= 200.01f);
        Assert.Equal(kin.Velocity.X, kin.Velocity.Y, precision: 2);
    }

    // =========================================================================
    // 2. TUNNELING & LARGE DELTA-TIME ADVERSARIAL STRESS TESTS
    // =========================================================================

    [Fact]
    public void Collision_Tunneling_DiscreteStepAcrossObstacleUnderExtremeDt()
    {
        // Wall at X in [200, 220], Y in [0, 500] (Width 20px)
        var world = new CollisionWorld();
        world.AddObstacle(new AABB(200f, 0f, 220f, 500f));

        var kin = new CharacterKinematics
        {
            Position = new Vector2(100f, 250f),
            Velocity = new Vector2(200f, 0f),
            FootHalfWidth = 12f,
            FootHeight = 14f
        };

        // If dt is huge (e.g. 1.0s) and velocity is 200 px/s, step is 200px:
        // Position jumps from 100 to 300. Character box at 300 is [288, 312], which does not overlap [200, 220].
        // This test verifies whether CollisionWorld without sub-stepping permits tunneling under large dt.
        Vector2 vel = new(200f, 0f);
        Vector2 endPos = world.MoveAndSlide(kin.Position, kin.FootHalfWidth, kin.FootHeight, ref vel, 1.0f);

        // Documented finding: Without swept AABB or sub-stepping, endPos tunnels past obstacle (endPos.X > 220).
        // Under production Program.cs, dt is capped at 0.1s (max step 20px).
        // Let's check both unconstrained dt and production-capped dt:
        bool tunneledUnder1sDt = endPos.X > 220f;
        Assert.True(tunneledUnder1sDt, "Expected discrete AABB MoveAndSlide to demonstrate tunneling under unconstrained 1.0s dt");
    }

    [Fact]
    public void Collision_ProductionCappedDt_PreventsTunnelingThroughStandardObstacles()
    {
        // In Program.cs: if (dt > 0.1f) dt = 0.1f;
        // Standard obstacle thickness in Hanoi scene is at least 20px (tables/bikes) or 28px (stalls).
        // Character foot half width is 12px (total 24px).
        // Max step at 200 px/s * 0.1s = 20px.
        var world = new CollisionWorld();
        // Hanoi market stall obstacle: width 72px (MinX 300, MaxX 372)
        world.AddObstacle(new AABB(300f, 100f, 372f, 200f));

        var kin = new CharacterKinematics
        {
            Position = new Vector2(250f, 150f), // 50px away from obstacle
            Velocity = new Vector2(200f, 0f),
            FootHalfWidth = 12f,
            FootHeight = 14f
        };

        // Simulate 20 frames with max allowable dt = 0.1s
        for (int i = 0; i < 20; i++)
        {
            kin.Update(new Vector2(1f, 0f), world, 0.1f);
        }

        // Must NOT tunnel through! Must stop at 300 - 12 - 0.01 = 287.99
        Assert.True(kin.Position.X <= 288.0f, $"Player penetrated or tunneled through stall! Pos: {kin.Position.X}");
        Assert.Equal(287.99f, kin.Position.X, precision: 1);
    }

    [Fact]
    public void Collision_MapBounds_PlayerNeverEscapesRegardlessOfExtremeDt()
    {
        var world = new CollisionWorld
        {
            MapBounds = new AABB(0f, 0f, 1600f, 900f)
        };

        Vector2 vel = new(50000f, 50000f);
        Vector2 pos = world.MoveAndSlide(new Vector2(800f, 450f), 12f, 14f, ref vel, 100.0f);

        // Clamping to map bounds is unconditional
        Assert.Equal(1600f - 12f, pos.X);
        Assert.Equal(900f, pos.Y);
        Assert.Equal(0f, vel.X);
        Assert.Equal(0f, vel.Y);
    }

    // =========================================================================
    // 3. COLLISION SLIDING: CONCAVE, ACUTE, SEAM, AND CHOKEPOINT CORNERS
    // =========================================================================

    [Fact]
    public void Collision_ConcaveInsideCorner_DiagonalInputStopsBothAxesCleanly()
    {
        var world = new CollisionWorld();
        // Inside corner formed by Vertical wall [300, 400] x [100, 400] and Horizontal wall [100, 400] x [300, 400]
        world.AddObstacle(new AABB(300f, 100f, 400f, 400f)); // Right vertical wall
        world.AddObstacle(new AABB(100f, 300f, 400f, 400f)); // Bottom horizontal floor

        var kin = new CharacterKinematics
        {
            Position = new Vector2(250f, 250f), // In the upper-left quadrant facing inside corner
            FootHalfWidth = 12f,
            FootHeight = 14f
        };

        // Move diagonally Down-Right (1, 1) straight into the inside corner
        for (int i = 0; i < 50; i++)
        {
            kin.Update(new Vector2(1f, 1f), world, 0.016f);
        }

        // Expected stop position:
        // X must stop at 300 - 12 - 0.01 = 287.99
        // Y must stop at 300 - 0.01 = 299.99
        Assert.Equal(287.99f, kin.Position.X, precision: 1);
        Assert.Equal(299.99f, kin.Position.Y, precision: 1);
        Assert.Equal(0f, kin.Velocity.X, precision: 1);
        Assert.Equal(0f, kin.Velocity.Y, precision: 1);

        // Now move diagonally UP-LEFT (-1, -1) out of the corner — MUST ESCAPE SMOOTHLY!
        for (int i = 0; i < 20; i++)
        {
            kin.Update(new Vector2(-1f, -1f), world, 0.016f);
        }

        Assert.True(kin.Position.X < 287.0f, $"Player stuck in inside corner on X: {kin.Position.X}");
        Assert.True(kin.Position.Y < 299.0f, $"Player stuck in inside corner on Y: {kin.Position.Y}");
    }

    [Fact]
    public void Collision_InternalSeamBetweenAdjacentObstacles_SmoothSlidingWithoutSnag()
    {
        var world = new CollisionWorld();
        // Two obstacles placed edge-to-edge seamlessly:
        // Obstacle 1: [100, 300] x [400, 500]
        // Obstacle 2: [300, 500] x [400, 500]
        // Seam is at X = 300
        world.AddObstacle(new AABB(100f, 400f, 300f, 500f));
        world.AddObstacle(new AABB(300f, 400f, 500f, 500f));

        var kin = new CharacterKinematics
        {
            Position = new Vector2(150f, 399.99f), // Pressed against top edge of Obstacle 1
            FootHalfWidth = 12f,
            FootHeight = 14f
        };

        // Move diagonally Down-Right (1, 1) sliding rightward across the seam at X = 300
        for (int i = 0; i < 100; i++)
        {
            kin.Update(new Vector2(1f, 1f), world, 0.016f);
        }

        // Character should have slid smoothly past seam X = 300 without snagging
        Assert.True(kin.Position.X > 320f, $"Character snagged at internal seam! Pos X: {kin.Position.X}");
        Assert.True(kin.Velocity.X > 140f, $"Velocity dropped at seam! Vel X: {kin.Velocity.X}");
        Assert.Equal(399.99f, kin.Position.Y, precision: 1);
    }

    [Fact]
    public void Collision_AcuteChokepoint_NarrowerThanPlayer_BlocksEntry()
    {
        var world = new CollisionWorld();
        // Left wall: [0, 100] x [0, 400]
        // Right wall: [120, 220] x [0, 400]
        // Gap width = 20px. Player foot width = 24px (FootHalfWidth = 12).
        world.AddObstacle(new AABB(0f, 0f, 100f, 400f));
        world.AddObstacle(new AABB(120f, 0f, 220f, 400f));

        var kin = new CharacterKinematics
        {
            Position = new Vector2(110f, 450f), // Centered below the 20px gap
            FootHalfWidth = 12f,
            FootHeight = 14f
        };

        // Try to push UP into the narrow chokepoint
        for (int i = 0; i < 50; i++)
        {
            kin.Update(new Vector2(0f, -1f), world, 0.016f);
        }

        // Must be blocked at obstacle bottom boundary: 400 + 14 + 0.01 = 414.01
        Assert.True(kin.Position.Y >= 414.0f, $"Player entered narrow chokepoint! Pos Y: {kin.Position.Y}");
        Assert.Equal(414.01f, kin.Position.Y, precision: 1);
    }

    // =========================================================================
    // 4. DYNAMIC Y-SORTING STRESS & ORACLE TESTS
    // =========================================================================

    private sealed class StressSortable : IYSortable
    {
        public string EntityId { get; init; } = "";
        public float FootY { get; init; }
        public int SubLayerPriority { get; init; }
    }

    [Fact]
    public void YSort_CollinearDozensOfEntities_SortOrderIsDeterministicAndStrict()
    {
        var queue = new YSortQueue<StressSortable>();
        int count = 200;

        // Generate 200 collinear entities at FootY = 500f with random priorities and IDs
        var rng = new Random(42);
        for (int i = 0; i < count; i++)
        {
            queue.Add(new StressSortable
            {
                EntityId = $"Entity_{i:D4}",
                FootY = 500.0f,
                SubLayerPriority = (i % 3) - 1 // -1, 0, 1
            });
        }

        queue.Sort();

        Assert.Equal(count, queue.Count);

        // Verify ordering oracle:
        // 1. All FootY equal to 500
        // 2. SubLayerPriority non-decreasing
        // 3. For identical SubLayerPriority, EntityId lexicographically non-decreasing
        for (int i = 0; i < count - 1; i++)
        {
            var a = queue.Entities[i];
            var b = queue.Entities[i + 1];

            Assert.Equal(500.0f, a.FootY);
            Assert.True(a.SubLayerPriority <= b.SubLayerPriority, $"Priority inversion at {i}: {a.SubLayerPriority} > {b.SubLayerPriority}");

            if (a.SubLayerPriority == b.SubLayerPriority)
            {
                int cmp = string.Compare(a.EntityId, b.EntityId, StringComparison.Ordinal);
                Assert.True(cmp <= 0, $"EntityId ordering inversion between {a.EntityId} and {b.EntityId}");
            }
        }
    }

    [Fact]
    public void YSort_PermutationInvariance_IdenticalResultRegardlessOfInsertionOrder()
    {
        var list1 = new List<StressSortable>();
        var list2 = new List<StressSortable>();

        var rng = new Random(1337);
        for (int i = 0; i < 150; i++)
        {
            var item = new StressSortable
            {
                EntityId = $"ID_{rng.Next(0, 50)}",
                FootY = (float)rng.Next(100, 110), // Heavy collisions on FootY
                SubLayerPriority = rng.Next(-1, 2)
            };
            list1.Add(item);
            list2.Add(item);
        }

        // Shuffle list2
        for (int i = list2.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list2[i], list2[j]) = (list2[j], list2[i]);
        }

        var queue1 = new YSortQueue<StressSortable>();
        queue1.AddRange(list1);
        queue1.Sort();

        var queue2 = new YSortQueue<StressSortable>();
        queue2.AddRange(list2);
        queue2.Sort();

        Assert.Equal(queue1.Count, queue2.Count);
        for (int i = 0; i < queue1.Count; i++)
        {
            Assert.Equal(queue1.Entities[i].EntityId, queue2.Entities[i].EntityId);
            Assert.Equal(queue1.Entities[i].FootY, queue2.Entities[i].FootY);
            Assert.Equal(queue1.Entities[i].SubLayerPriority, queue2.Entities[i].SubLayerPriority);
        }
    }

    // =========================================================================
    // 5. CAMERA 2D TRACKER STRESS TESTS
    // =========================================================================

    [Fact]
    public void Camera_SuddenTeleportation_SmoothlyCatchesUpWithoutOvershoot()
    {
        var camera = new Camera2DTracker
        {
            ViewportSize = new Vector2(1280f, 720f),
            MapBounds = new AABB(0f, 0f, 5000f, 5000f),
            FollowSmoothRate = 8.0f
        };

        camera.SnapToTarget(new Vector2(640f, 360f));

        // Sudden teleport to (4000, 4000)
        Vector2 teleportTarget = new(4000f, 4000f);

        // Step over 100 ticks (1.6s total at 60 FPS)
        Vector2 prevPos = camera.CurrentPosition;
        for (int i = 0; i < 100; i++)
        {
            camera.Update(teleportTarget, 0.016f);

            // Camera must move monotonically towards target
            Assert.True(camera.CurrentPosition.X >= prevPos.X);
            Assert.True(camera.CurrentPosition.Y >= prevPos.Y);
            Assert.True(camera.CurrentPosition.X <= teleportTarget.X);
            Assert.True(camera.CurrentPosition.Y <= teleportTarget.Y);

            prevPos = camera.CurrentPosition;
        }

        // After ~1.6s at rate 8.0, distance should have snapped or be extremely close
        Assert.Equal(teleportTarget.X, camera.CurrentPosition.X, precision: 1);
        Assert.Equal(teleportTarget.Y, camera.CurrentPosition.Y, precision: 1);
    }

    [Fact]
    public void Camera_SubViewportMap_AlwaysCentered()
    {
        var camera = new Camera2DTracker
        {
            ViewportSize = new Vector2(1280f, 720f),
            MapBounds = new AABB(0f, 0f, 800f, 600f) // Smaller than viewport on both axes
        };

        // Any target position should clamp to center of map (400, 300)
        Vector2 clamped1 = camera.ClampCamera(new Vector2(100f, 100f));
        Vector2 clamped2 = camera.ClampCamera(new Vector2(900f, 900f));
        Vector2 clamped3 = camera.ClampCamera(new Vector2(-500f, -500f));

        Assert.Equal(new Vector2(400f, 300f), clamped1);
        Assert.Equal(new Vector2(400f, 300f), clamped2);
        Assert.Equal(new Vector2(400f, 300f), clamped3);
    }

    [Fact]
    public void Camera_ExtremeZoom_MaintainsMapClamping()
    {
        var camera = new Camera2DTracker
        {
            ViewportSize = new Vector2(1280f, 720f),
            MapBounds = new AABB(0f, 0f, 2000f, 1200f),
            Zoom = 2.0f // Half world viewport (640 x 360)
        };

        // Half extents are (320, 180). Snapping to (0, 0) clamps to (320, 180)
        camera.SnapToTarget(new Vector2(0f, 0f));
        Assert.Equal(new Vector2(320f, 180f), camera.CurrentPosition);

        // Snapping to (2000, 1200) clamps to (1680, 1020)
        camera.SnapToTarget(new Vector2(2000f, 1200f));
        Assert.Equal(new Vector2(1680f, 1020f), camera.CurrentPosition);

        var bounds = camera.GetVisibleWorldBounds();
        Assert.True(bounds.MinX >= 0f);
        Assert.True(bounds.MinY >= 0f);
        Assert.True(bounds.MaxX <= 2000f);
        Assert.True(bounds.MaxY <= 1200f);
    }

    [Fact]
    public void Collision_OverlappingObstacles_ContinuousSlide()
    {
        var world = new CollisionWorld();
        // Obstacle 1: [100, 300] x [300, 400]
        // Obstacle 2: [250, 450] x [300, 400] (Overlaps between 250 and 300)
        world.AddObstacle(new AABB(100f, 300f, 300f, 400f));
        world.AddObstacle(new AABB(250f, 300f, 450f, 400f));

        var kin = new CharacterKinematics
        {
            Position = new Vector2(150f, 299.99f), // Pressed against top
            FootHalfWidth = 12f,
            FootHeight = 14f
        };

        // Slide diagonally across the overlapping zone
        for (int i = 0; i < 100; i++)
        {
            kin.Update(new Vector2(1f, 1f), world, 0.016f);
        }

        // Must slide past the overlap zone (pos.X > 320) while remaining clamped on Y (299.99)
        Assert.True(kin.Position.X > 320f, $"Stuck in overlapping obstacles! Pos X: {kin.Position.X}");
        Assert.Equal(299.99f, kin.Position.Y, precision: 1);
    }

    [Fact]
    public void YSort_MassiveLoad_10000Entities_DeterministicAndFast()
    {
        var queue = new YSortQueue<StressSortable>();
        int count = 10000;
        var rng = new Random(777);

        for (int i = 0; i < count; i++)
        {
            queue.Add(new StressSortable
            {
                EntityId = $"Entity_{i:D6}",
                FootY = (float)rng.Next(0, 1000),
                SubLayerPriority = rng.Next(-1, 2)
            });
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        queue.Sort();
        sw.Stop();

        Assert.Equal(count, queue.Count);
        Assert.True(sw.ElapsedMilliseconds < 500, $"Sorting 10k entities took too long: {sw.ElapsedMilliseconds} ms");

        for (int i = 0; i < count - 1; i++)
        {
            var a = queue.Entities[i];
            var b = queue.Entities[i + 1];

            if (a.FootY != b.FootY)
            {
                Assert.True(a.FootY < b.FootY);
            }
            else if (a.SubLayerPriority != b.SubLayerPriority)
            {
                Assert.True(a.SubLayerPriority < b.SubLayerPriority);
            }
            else
            {
                Assert.True(string.Compare(a.EntityId, b.EntityId, StringComparison.Ordinal) <= 0);
            }
        }
    }
}
