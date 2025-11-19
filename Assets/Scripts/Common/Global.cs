using System;
using System.Collections.Generic;
using NetworkMessageProto;
using UnityEngine;
public enum MessageType
{
    Unset = 0,
    FullTransformState = 1,
    TransformStateUpdate,
    AbilityStateUpdate,
    Input,
    PlayersUpdate,
    LearnSkill,
};

public enum MessageTarget
{
    // 本地消息，只有自己发送给自己会是Local
    Local = 0,
    // 发送给所有人，包括Host自己
    All,
    // 发送给除了自己以外的所有人
    Others,
    // 发送给Host
    Host,
}

public enum AttrChangeType
{
    Absolute = 0,
    Relative = 1,
}

public enum TileType
{
    // 不碰撞
    Floor = 0,
    Floor_Boss = 5,
    // 碰撞，不可摧毁
    Wall_Horizontal = 1,
    Wall_Vertical = 2,
    // 只和人物碰撞，不和子弹/飞行物体碰撞
    Hole = 3,
    // 碰撞，可摧毁
    BreakableObstacle = 4,
    // 碰撞，不可摧毁
    UnbreakableObstacle = 6,
    // 仅用于半透明覆盖在Floor上，不碰撞
    Highlight = 10,
}

public enum CharacterType
{
    Unset = 0,
    // 默认的角色，最终也会变成Boss
    Contra_Bill = 1,
    // Level 1 0, 踩踏者
    Minion_1_0_Stomper,
    // Level 1 1, 爆破小子
    Minion_1_1_BusterBot,
    // Elite 精英
    // Boss
    Boss_1_0_PhantomTank,
    Minion_2_0_GlitchSlime,
    Minion_2_1_SpikeTurtle,
    Boss_2_0_MasterTurtle,
    Minion_3_0_PixelBat,
    Minion_3_1_SkeletonMage,
    Boss_3_0_PokeBoy,
    Minion_4_0_HealerSprite,
    Minion_4_1_KamikazeShip,
    Boss_4_0_SysGuardian,
    Boss_5_0_TheRuler,
    Boss_4_0_SysGuardianFloatingTurret,
    Minion_9_Wanderer,
    Minion_10_RedChaser,
    Minion_11_TurretFlower,
    Minion_12_DashCar,
    Minion_13_BoomMan,
}

public enum ItemTag
{
    Default = 0,
    PhantomTank,
    MasterLong,
    Pokeboy,
    Elite,
    NotDrop, // 少部分特殊道具，只会在特殊条件达到后生成，而不会随机掉落
}

public enum DamageType
{
    Bullet = 0,
    Collision,
    Capture,
}

public enum Direction
{
    Down = 0,
    Left,
    Up,
    Right,
}

public enum GameState
{
    InMenu = 0,
    InGame = 1,
}

public static class Constants
{
    public const string AIPlayerPrefix = "BGBGAI_";
    public const int MinPlayableObjects = 1;
    public const int CharacterMaxWidth = 4;
    public const int CharacterMaxHeight = 4;
    public const int RoomStep = 20;
    public const int DoorWidth = 4;
    public const int DoorMin = RoomStep / 2 - DoorWidth / 2;
    public const int DoorMax = RoomStep / 2 + DoorWidth / 2;
    public const float CharacterMaxRadius = 2f;
    public const float WallMaxThickness = 1.2f;
    // 每升1级，从3个技能中选择1个
    public const int SkillChooseNumber = 3;
    public const string TagPlayer = "Player";
    public const string TagPlayerFeet = "PlayerFeet";
    public const string TagEnemy = "Enemy";
    public const string TagWall = "Wall";
    public const string TagShield = "Shield";
    public const string SummonBossName = "SummonBoss";
    public const int SingularityItemId = 2;
    public const int SysBugItemId = 3;
    public const int HealthRecoverySkillId = 8;
    public const int PhantomChargeSkillId = 9;
    public const int MasterLongWaveSkillId = 10;
    public const int LogFragmentSkillId = 11;
    public const int CompanionMasterSkillId = 12;
    public const int NewRulerPlayerId = 123456789;
    public const float Eps = 0.00001f;
    public const float BossHpMultipiler = 20f;

    public static Dictionary<GameObject, CharacterStatus> goToCharacterStatus = new();
    public static Dictionary<GameObject, CharacterInput> goToCharacterInput = new();
    public static readonly int[] LevelUpExp = {
        100,
        160,
        240,
        350,
        480,
        630,
        800,
        1000,
        1250,
        2500,
        5000,
        10000,
    };

    public static readonly float[] DamageLevel =
    {
        1.69f / 3.5f, // 0.48285714
        2.83f / 3.5f, // 0.8085714
        4.21f / 3.5f, // 1.202857
        5.84f / 3.5f, // 1.6685714
        7.69f / 3.5f, // 2.197142857
        9.77f / 3.5f, // 2.79142857
        12.06f / 3.5f, // 3.445714
        float.MaxValue,
    };

    public static readonly float[] AtkFreqLevel =
    {
        1.58f,
        2.32f,
        3.13f,
        3.99f,
        4.91f,
        5.86f,
        6.86f,
        float.MaxValue,
    };

    public static readonly float[] AtkRangeLevel =
    {
        4.24f,
        5.74f,
        7.24f,
        8.74f,
        10.24f,
        11.74f,
        13.24f,
        float.MaxValue,
    };

    public static readonly float[] BulletSpeedLevel =
    {
        6.9f,//0.69f,
        8.9f,//0.89f,
        10.9f,//1.09f,
        12.9f,//1.29f,
        14.9f,//1.49f,
        16.9f,//1.69f,
        18.9f,//1.89f,
        float.MaxValue,
    };

    public static readonly float[] MoveSpeedLevel =
    {
        6.6f,//0.66f,
        8.8f,//0.88f,
        11.1f,//1.11f,
        13.3f,//1.33f,
        15.5f,//1.55f,
        17.7f,//1.77f,
        19.9f,//1.99f,
        float.MaxValue,
    };

    public static int GetStatLevel(float stat, float[] levelArray)
    {
        for (int i = 0; i < levelArray.Length; i++)
        {
            if (stat < levelArray[i])
            {
                return i;
            }
        }
        return levelArray.Length;
    }

    public static int PositiveMod(this int a, int n)
    {
        if (n <= 0) throw new ArgumentException("模数必须为正数", nameof(n));
        int r = a % n;
        return r >= 0 ? r : r + n;
    }

    public static float PositiveMod(this float a, float n)
    {
        if (n <= 0) throw new ArgumentException("模数必须为正数", nameof(n));
        float r = a % n;
        return r >= 0 ? r : r + n;
    }

    public static Color RandomColor()
    {
        // 更偏向于右上角比较明显的颜色
        Color color = new Color();
        color.a = 1;
        int rnd = UnityEngine.Random.Range(0, 3);
        if (rnd == 0)
        {
            if (UnityEngine.Random.value > 0.5f)
            {
                color.r = 1;
                color.g = 0;
            }
            else
            {
                color.r = 0;
                color.g = 1;
            }
            color.b = UnityEngine.Random.Range(0, 1f);
        }
        else if (rnd == 1)
        {
            if (UnityEngine.Random.value > 0.5f)
            {
                color.g = 1;
                color.b = 0;
            }
            else
            {
                color.g = 0;
                color.b = 1;
            }
            color.r = UnityEngine.Random.Range(0, 1f);
        }
        else // if (rnd == 2)
        {
            if (UnityEngine.Random.value > 0.5f)
            {
                color.b = 1;
                color.r = 0;
            }
            else
            {
                color.b = 0;
                color.r = 1;
            }
            color.g = UnityEngine.Random.Range(0, 1f);
        }
        return color;
    }

    public static Color ToColor(this ColorProto c)
    {
        if (c == null) return Color.white;
        return new Color(c.R, c.G, c.B, c.A);
    }

    public static bool CompareThisAndParentTag(this GameObject obj, string tag)
    {
        if (obj.CompareTag(tag)) return true;
        if (obj.transform.parent != null) return obj.transform.parent.CompareTag(tag);
        return false;
    }

    public static void PositionToIndex(Vector2 position, out int x, out int y)
    {
        if (position.x < 0 || position.y < 0)
        {
            x = -1;
            y = -1;
        }
        else
        {
            x = (int)position.x / RoomStep;
            y = (int)position.y / RoomStep;
        }
    }

    public static bool IsZero(this float f)
    {
        return Mathf.Abs(f) < Eps;
    }

    public static bool MyCompareTag(this Collider2D other, string tag)
    {
        Rigidbody2D hitRigidbody = other.attachedRigidbody;
        if (hitRigidbody != null)
        {
            return hitRigidbody.gameObject.CompareTag(tag);
        }
        return false;
    }

    public static bool IsPlayerOrEnemy(this Collider2D other)
    {
        if (other.CompareTag(TagPlayerFeet)) return false;
        return other.MyCompareTag(TagPlayer) || other.MyCompareTag(TagEnemy);
    }

    public static bool IsPlayerOrEnemy(this GameObject obj)
    {
        return obj.CompareThisAndParentTag(TagPlayer) || obj.CompareThisAndParentTag(TagEnemy);
    }

    public static bool IsFriendlyUnit(this CharacterStatus myStatus, CharacterStatus tarStatus)
    {
        return myStatus == tarStatus
            // 对方是我的Trainer 或者 我是对方的Trainer
            || tarStatus == myStatus.Trainer || tarStatus.Trainer == myStatus
            // 或者 对方是同一个Trainer下的队友
            || (tarStatus.Trainer != null && tarStatus.Trainer == myStatus.Trainer)
            // 或者 都是enemy，enemy之间不互相伤害
            || (tarStatus.gameObject.CompareThisAndParentTag(TagEnemy) && myStatus.gameObject.CompareThisAndParentTag(TagEnemy));
    }

    // public static bool IsAllEnemy(this CharacterStatus myStatus, CharacterStatus tarStatus)
    // {
    //     return tarStatus != null && myStatus != null
    //         && tarStatus.gameObject.CompareThisAndParentTag(TagEnemy)
    //         && myStatus.gameObject.CompareThisAndParentTag(TagEnemy);
    // }

    public static CharacterStatus GetCharacterStatus(this GameObject obj)
    {
        if (goToCharacterStatus.TryGetValue(obj, out CharacterStatus status))
        {
            return status;
        }
        if (obj.TryGetComponent<CharacterStatus>(out status))
        {
            goToCharacterStatus[obj] = status;
        }
        return status;
    }

    public static CharacterInput GetCharacterInput(this GameObject obj)
    {
        if (goToCharacterInput.TryGetValue(obj, out CharacterInput input))
        {
            return input;
        }
        if (obj.TryGetComponent<CharacterInput>(out input))
        {
            goToCharacterInput[obj] = input;
        }
        return input;
    }

    public static CharacterStatus GetCharacterStatus(this Collider2D other)
    {
        Rigidbody2D hitRigidbody = other.attachedRigidbody;
        if (hitRigidbody != null)
        {
            return hitRigidbody.gameObject.GetCharacterStatus();
        }
        return null;
    }
    
    public static CharacterStatus GetCharacterStatus(this Collision2D collision)
    {
        return collision.gameObject.GetCharacterStatus();
    }

    public static float GetFinalDamage(this PlayerState state, float damage)
    {
        return damage * Mathf.Sqrt(1f + 1.2f * state.DamageUp);
    }

    public static float GetFinalAtkFreq(this PlayerState state)
    {
        float delay = 10;
        if (state.AttackFreqUp >= 425f / 234f)
        {
            delay = 5;
        } 
        else if (state.AttackFreqUp >= 0)
        {
            delay = 16f - 6f * Mathf.Sqrt(1.3f * state.AttackFreqUp + 1);
        }
        else if (state.AttackFreqUp >= -10f / 13f)
        {
            delay = 16f - 6f * Mathf.Sqrt(1.3f * state.AttackFreqUp + 1) - 6 * state.AttackFreqUp;
        }
        else
        {
            delay = 16 - 6 * state.AttackFreqUp;
        }

        return 30 / (delay + 1);
    }
}