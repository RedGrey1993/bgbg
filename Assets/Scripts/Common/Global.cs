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
    public const int RoomStep = 20;
    public const int DoorWidth = 4;
    public const int DoorMin = RoomStep / 2 - DoorWidth / 2;
    public const int DoorMax = RoomStep / 2 + DoorWidth / 2;
    public const float CharacterMaxRadius = 1f;
    public const float WallMaxThickness = 1.2f;
    // 每升1级，从3个技能中选择1个
    public const int SkillChooseNumber = 3;
    public const string TagPlayer = "Player";
    public const string TagPlayerFeet = "PlayerFeet";
    public const string TagEnemy = "Enemy";
    public const string TagWall = "Wall";
    public const string TagShield = "Shield";
    public const int SysBugItemId = 3;
    public const int HealthRecoverySkillId = 8;
    public const int PhantomChargeSkillId = 9;
    public const int MasterLongWaveSkillId = 10;
    public const int LogFragmentSkillId = 11;
    public const int NewRulerPlayerId = 123456789;
    public const float Eps = 0.00001f;

    public static Dictionary<GameObject, CharacterStatus> goToCharacterStatus = new Dictionary<GameObject, CharacterStatus>();
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
}