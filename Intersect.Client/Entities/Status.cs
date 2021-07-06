﻿using System;

using Intersect.Client.General;
using Intersect.Enums;

namespace Intersect.Client.Entities
{

    public partial class Status
    {

        public string Data = "";

        public int[] Shield = new int[(int) Vitals.VitalCount];

        public Guid SpellId;

        public long TimeRecevied = 0;

        public long TimeRemaining = 0;

        public long TotalDuration = 1;

        public StatusTypes Type;

        public bool IsPassive = false;

        public Status(Guid spellId, StatusTypes type, string data, long timeRemaining, long totalDuration, bool isPassive = false)
        {
            SpellId = spellId;
            Type = type;
            Data = data;
            TimeRemaining = timeRemaining;
            TotalDuration = totalDuration;
            TimeRecevied = Globals.System.GetTimeMs();
            IsPassive = isPassive;

        }

        public bool IsActive()
        {
            if (isPassive())
            {
                return TotalDuration >= 0;
            }

            return RemainingMs() > 0;
        }

        public long RemainingMs()
        {
            var timeDiff = Globals.System.GetTimeMs() - TimeRecevied;

            return TimeRemaining - timeDiff;
        }

        public bool isPassive()
        {
            return IsPassive;
        }

    }

}
