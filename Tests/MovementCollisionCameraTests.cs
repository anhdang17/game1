using System;
using System.Numerics;
using GameProject.Core.Movement;
using GameProject.Core.Spatial;
using Xunit;

namespace GameProject.Tests;

public class MovementCollisionCameraTests
{
    // =========================================================================
    // 1. CHARACTER KINEMATICS TESTS
    // =========================================================================

    [Fact]
    public void Kinematics_MoveRight_AcceleratesUpToMaxSpeed()
    {
        var world = new CollisionWorld();
        var kin = new CharacterKinematics
        {
            Position = new Vector2(100f, 100f),
            MaxSpeed = 200f,
            Acceleration = 1200f
        };

        // Update with full right input across several ticks (0.2s total)
        for (int i = 0; i < 20; i++)
        {
            kin.Update(new Vector2(1f, 0f), world, 0.01f);
        }

        Assert.Equal(200f, kin.Velocity.X, precision: 1);
        Assert.Equal(0f, kin.Velocity.Y, precision: 2);
        Assert.True(kin.Position.X > 100f);
        Assert.Equal(FacingDirection.Right, kin.Facing);
    }

    [Fact]
    public void Kinematics_DiagonalMove_NormalizedToMaxSpeed()
    {
        var world = new CollisionWorld();
        var kin = new CharacterKinematics
        {
            Position = new Vector2(100f, 100f),
            MaxSpeed = 200f,
            Acceleration = 1200f
        };

        // Move diagonally (1, 1)
        for (int i = 0; i < 30; i++)
        {
            kin.Update(new Vector2(1f, 1f), world, 0.01f);
        }

        float speed = kin.Velocity.Length();
        Assert.True(speed <= 200.01f, $"Speed was {speed}, exceeding max speed 200!");
        Assert.Equal(200f, speed, precision: 1);
        Assert.Equal(kin.Velocity.X, kin.Velocity.Y, precision: 2);
    }

    [Fact]
    public void Kinematics_OpposingInputs_CancelsToZeroWithFriction()
    {
        var world = new CollisionWorld();
        var kin = new CharacterKinematics
        {
            Position = new Vector2(100f, 100f),
            Velocity = new Vector2(200f, 0f),
            MaxSpeed = 200f,
            Friction = 1500f
        };

        // No input direction (simulating release or opposing keys cancellation)
        for (int i = 0; i < 20; i++)
        {
            kin.Update(Vector2.Zero, world, 0.01f);
        }

        Assert.Equal(Vector2.Zero, kin.Velocity);
        Assert.False(kin.IsMoving);
    }

    [Theory]
    [InlineData(1f, 0f, FacingDirection.Right)]
    [InlineData(-1f, 0f, FacingDirection.Left)]
    [InlineData(0f, 1f, FacingDirection.Down)]
    [InlineData(0f, -1f, FacingDirection.Up)]
    public void Kinematics_FacingDirection_TracksDominantVelocityAxis(float ix, float iy, FacingDirection expectedFacing)
    {
        var world = new CollisionWorld();
        var kin = new CharacterKinematics { Position = new Vector2(200f, 200f) };

        for (int i = 0; i < 10; i++)
        {
            kin.Update(new Vector2(ix, iy), world, 0.01f);
        }

        Assert.Equal(expectedFacing, kin.Facing);
    }

    [Fact]
    public void Kinematics_WalkCycle_BobbingAndFootstepTriggers()
    {
        var world = new CollisionWorld();
        var kin = new CharacterKinematics
        {
            Position = new Vector2(200f, 200f),
            WalkAnimationFrequency = 4.0f
        };

        // Initially stationary -> bobbing is 0, no footstep
        Assert.Equal(0f, kin.GetVerticalBobbingOffset());
        Assert.False(kin.CheckFootstepTrigger());

        // Moving right
        bool footstepFired = false;
        for (int i = 0; i < 100; i++)
        {
            kin.Update(new Vector2(1f, 0f), world, 0.01f);
            if (kin.CheckFootstepTrigger())
            {
                footstepFired = true;
            }
            float bob = kin.GetVerticalBobbingOffset();
            Assert.True(MathF.Abs(bob) <= 2.51f, $"Bobbing offset {bob} exceeded max 2.5f");
        }

        Assert.True(footstepFired, "Expected at least one footstep trigger during walk cycle!");
    }

    // =========================================================================
    // 2. COLLISION WORLD & SLIDE-ALONG-WALL TESTS
    // =========================================================================

    [Fact]
    public void Collision_DirectMoveIntoWall_StopsAtBoundary()
    {
        var world = new CollisionWorld();
        // Obstacle wall at X: [300, 400], Y: [100, 400]
        world.AddObstacle(new AABB(300f, 100f, 400f, 400f));

        var kin = new CharacterKinematics
        {
            Position = new Vector2(280f, 250f),
            FootHalfWidth = 12f,
            FootHeight = 14f
        };

        // Try to move right through the wall
        for (int i = 0; i < 30; i++)
        {
            kin.Update(new Vector2(1f, 0f), world, 0.016f);
        }

        // Expected stop position: obstacle.MinX (300) - FootHalfWidth (12) - Epsilon (0.01) = 287.99
        Assert.True(kin.Position.X <= 288.0f, $"Player penetrated wall: X was {kin.Position.X}");
        Assert.Equal(287.99f, kin.Position.X, precision: 1);
        Assert.Equal(0f, kin.Velocity.X, precision: 1);
    }

    [Fact]
    public void Collision_SlideAlongVerticalWall_DiagonalMaintainsSlidingAxis()
    {
        var world = new CollisionWorld();
        // Vertical wall to the right: X in [300, 400], Y in [0, 1000]
        world.AddObstacle(new AABB(300f, 0f, 400f, 1000f));

        var kin = new CharacterKinematics
        {
            Position = new Vector2(287.99f, 200f), // Already pressed against wall
            FootHalfWidth = 12f,
            FootHeight = 14f
        };

        float initialY = kin.Position.Y;

        // Move diagonally Down-Right (1, 1) against the wall
        for (int i = 0; i < 30; i++)
        {
            kin.Update(new Vector2(1f, 1f), world, 0.016f);
        }

        // X must remain clamped at wall boundary (cannot penetrate)
        Assert.True(kin.Position.X <= 288.0f);

        // Y must smoothly advance downwards without obstruction!
        Assert.True(kin.Position.Y > initialY + 50f, $"Player failed to slide on Y! Y moved from {initialY} to {kin.Position.Y}");
    }

    [Fact]
    public void Collision_SlideAlongHorizontalWall_DiagonalMaintainsHorizontalAxis()
    {
        var world = new CollisionWorld();
        // Horizontal wall below: X in [0, 1000], Y in [500, 600]
        world.AddObstacle(new AABB(0f, 500f, 1000f, 600f));

        var kin = new CharacterKinematics
        {
            Position = new Vector2(200f, 499.99f), // Pressed against top of wall
            FootHalfWidth = 12f,
            FootHeight = 14f
        };

        float initialX = kin.Position.X;

        // Move diagonally Down-Right (1, 1) into horizontal wall
        for (int i = 0; i < 30; i++)
        {
            kin.Update(new Vector2(1f, 1f), world, 0.016f);
        }

        // Y must remain clamped at top boundary of obstacle
        Assert.True(kin.Position.Y <= 500.0f);

        // X must smoothly advance rightwards!
        Assert.True(kin.Position.X > initialX + 50f, $"Player failed to slide on X! X moved from {initialX} to {kin.Position.X}");
    }

    [Fact]
    public void Collision_MapBoundsClamping_PlayerNeverLeavesMap()
    {
        var world = new CollisionWorld
        {
            MapBounds = new AABB(0f, 0f, 500f, 400f)
        };

        var kin = new CharacterKinematics
        {
            Position = new Vector2(100f, 100f),
            FootHalfWidth = 12f,
            FootHeight = 14f
        };

        // Push player towards negative infinity (up-left)
        for (int i = 0; i < 50; i++)
        {
            kin.Update(new Vector2(-1f, -1f), world, 0.05f);
        }

        Assert.Equal(12f, kin.Position.X); // MinX + FootHalfWidth
        Assert.Equal(14f, kin.Position.Y); // MinY + FootHeight

        // Push player towards positive infinity (down-right)
        for (int i = 0; i < 100; i++)
        {
            kin.Update(new Vector2(1f, 1f), world, 0.05f);
        }

        Assert.Equal(488f, kin.Position.X); // MaxX - FootHalfWidth
        Assert.Equal(400f, kin.Position.Y); // MaxY
    }

    // =========================================================================
    // 3. 2D CAMERA TRACKER TESTS
    // =========================================================================

    [Fact]
    public void Camera_ClampsToMapBounds_NeverExposesOuterVoid()
    {
        var camera = new Camera2DTracker
        {
            ViewportSize = new Vector2(1280f, 720f),
            MapBounds = new AABB(0f, 0f, 2000f, 1200f)
        };

        // Half viewport is (640, 360)
        // Camera snapped to (0, 0) should clamp to (640, 360)
        camera.SnapToTarget(new Vector2(0f, 0f));
        Assert.Equal(new Vector2(640f, 360f), camera.CurrentPosition);

        // Snapped to (2000, 1200) should clamp to (1360, 840)
        camera.SnapToTarget(new Vector2(2000f, 1200f));
        Assert.Equal(new Vector2(1360f, 840f), camera.CurrentPosition);

        var bounds = camera.GetVisibleWorldBounds();
        Assert.True(bounds.MinX >= 0f);
        Assert.True(bounds.MinY >= 0f);
        Assert.True(bounds.MaxX <= 2000f);
        Assert.True(bounds.MaxY <= 1200f);
    }

    [Fact]
    public void Camera_ExponentialLerp_SmoothlyApproachesTarget()
    {
        var camera = new Camera2DTracker
        {
            ViewportSize = new Vector2(1280f, 720f),
            MapBounds = new AABB(0f, 0f, 3000f, 3000f),
            FollowSmoothRate = 8.0f
        };

        camera.SnapToTarget(new Vector2(1000f, 1000f));
        Assert.Equal(new Vector2(1000f, 1000f), camera.CurrentPosition);

        // Move target to (1500, 1500)
        camera.Update(new Vector2(1500f, 1500f), 0.05f);

        // Position should have advanced towards target without instantly teleporting
        Assert.True(camera.CurrentPosition.X > 1000f);
        Assert.True(camera.CurrentPosition.X < 1500f);
        Assert.Equal(camera.CurrentPosition.X, camera.CurrentPosition.Y);
    }

    // =========================================================================
    // 4. DYNAMIC Y-SORTING TESTS
    // =========================================================================

    private sealed class MockYSortable : IYSortable
    {
        public string EntityId { get; init; } = "";
        public float FootY { get; init; }
        public int SubLayerPriority { get; init; }
    }

    [Fact]
    public void YSort_EntitiesSortedByFootY_Ascending()
    {
        var queue = new YSortQueue<MockYSortable>();
        queue.Add(new MockYSortable { EntityId = "Stall_Pho", FootY = 300f });
        queue.Add(new MockYSortable { EntityId = "BeMay", FootY = 250f });
        queue.Add(new MockYSortable { EntityId = "Cyclo", FootY = 400f });

        queue.Sort();

        Assert.Equal("BeMay", queue.Entities[0].EntityId);
        Assert.Equal("Stall_Pho", queue.Entities[1].EntityId);
        Assert.Equal("Cyclo", queue.Entities[2].EntityId);
    }

    [Fact]
    public void YSort_IdenticalFootY_DeterministicTieBreaking()
    {
        var queue = new YSortQueue<MockYSortable>();

        // 3 entities at exactly FootY = 300f
        // Sublayer -1 (Shadow), Sublayer 0 (Body "Z"), Sublayer 0 (Body "A"), Sublayer 1 (Companion)
        queue.Add(new MockYSortable { EntityId = "Entity_Z", FootY = 300f, SubLayerPriority = 0 });
        queue.Add(new MockYSortable { EntityId = "Companion_ButChi", FootY = 300f, SubLayerPriority = 1 });
        queue.Add(new MockYSortable { EntityId = "Entity_A", FootY = 300f, SubLayerPriority = 0 });
        queue.Add(new MockYSortable { EntityId = "Player_Shadow", FootY = 300f, SubLayerPriority = -1 });

        queue.Sort();

        Assert.Equal("Player_Shadow", queue.Entities[0].EntityId); // Priority -1
        Assert.Equal("Entity_A", queue.Entities[1].EntityId);      // Priority 0, alphabetical A
        Assert.Equal("Entity_Z", queue.Entities[2].EntityId);      // Priority 0, alphabetical Z
        Assert.Equal("Companion_ButChi", queue.Entities[3].EntityId); // Priority 1
    }

    [Fact]
    public void AABB_OverlapsAndContains_BoundaryCases()
    {
        var box1 = new AABB(10f, 10f, 50f, 50f);
        var box2 = new AABB(49f, 49f, 100f, 100f); // Overlapping by 1px
        var box3 = new AABB(50f, 50f, 100f, 100f); // Touching edge exactly (open interval -> no overlap)

        Assert.True(box1.Overlaps(box2));
        Assert.False(box1.Overlaps(box3));

        Assert.True(box1.Contains(new Vector2(10f, 10f)));
        Assert.True(box1.Contains(new Vector2(50f, 50f)));
        Assert.True(box1.Contains(new Vector2(30f, 30f)));
        Assert.False(box1.Contains(new Vector2(9.9f, 10f)));
    }

    [Fact]
    public void Camera_MapSmallerThanViewport_CentersViewport()
    {
        var camera = new Camera2DTracker
        {
            ViewportSize = new Vector2(1280f, 720f),
            MapBounds = new AABB(100f, 100f, 500f, 400f) // Map width 400 < 1280, height 300 < 720
        };

        var pos = camera.ClampCamera(new Vector2(0f, 0f));
        // Center of map is ( (100+500)/2, (100+400)/2 ) = (300, 250)
        Assert.Equal(new Vector2(300f, 250f), pos);
    }
}
