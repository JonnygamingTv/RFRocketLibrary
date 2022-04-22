﻿using System;
using SDG.Unturned;

namespace RFRocketLibrary.Utils
{
    public static class BarricadeUtil
    {
        #region Methods

        public static void ChangeOwnerAndGroup(EBuild build, ulong owner, ulong group, ref byte[] state)
        {
            switch (build)
            {
                case EBuild.DOOR or EBuild.GATE or EBuild.SHUTTER or EBuild.HATCH or EBuild.MANNEQUIN or EBuild.STORAGE
                    or EBuild.STORAGE_WALL or EBuild.SIGN or EBuild.SIGN_WALL or EBuild.NOTE:
                {
                    BitConverter.GetBytes(owner).CopyTo(state, 0);
                    BitConverter.GetBytes(group).CopyTo(state, 8);
                    break;
                }
                case EBuild.BED:
                {
                    BitConverter.GetBytes(owner).CopyTo(state, 0);
                    break;
                }
            }
        }

        #endregion
    }
}