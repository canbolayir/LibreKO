using System;
using System.Collections.Generic;
using Godot;

namespace LibreKO;

// Order is precedence, not style: the first open entry wins. Insert deliberately, not at the end.
public partial class World : Node3D
{
    private readonly List<(Func<bool> IsOpen, Action Close)> _escapeStack = new();

    private void EscapeCloses(Func<bool> isOpen, Action close) => _escapeStack.Add((isOpen, close));

    private void BuildEscapeStack()
    {
        EscapeCloses(() => AimingAreaSkill, () => CancelAreaCast());
        EscapeCloses(() => _fullMapShown, () => ToggleFullMap());
        EscapeCloses(() => _questNotifications.Count > 0, () => DismissQuestNotifications());
        EscapeCloses(() => _npcDialogShown, () => CloseNpcDialog());
        EscapeCloses(() => _tradePrompt.Visible, () => _tradePrompt.Close());
        EscapeCloses(() => _vendorShown, () => CloseVendor());
        EscapeCloses(() => _repairShown, () => CloseRepair());
        EscapeCloses(() => _vipWhPinDlg.Visible, () => _vipWhPinDlg.Hide());
        EscapeCloses(() => _vipWhAmount.Visible, () => _vipWhAmount.Close());
        EscapeCloses(() => _clanWhAmount.Visible, () => _clanWhAmount.Close());
        EscapeCloses(() => _whAmount.Visible, () => _whAmount.Close());
        EscapeCloses(() => _whShown, () => CloseWarehouse());
        EscapeCloses(() => _upgradeShown, () => CloseUpgrade());
        EscapeCloses(() => _upgradeChoicePanel is { Visible: true }, () => _upgradeChoicePanel.Visible = false);
        EscapeCloses(() => _classChangeShown, () => CloseClassChange());
        EscapeCloses(() => _genderShown, CloseGenderChange);
        EscapeCloses(() => _transferShown, CancelNationTransfer);
        EscapeCloses(() => _merchantSearchShown, CloseMerchantSearch);
        EscapeCloses(() => _exFinalPending, CloseExchangeFinal);
        EscapeCloses(() => _exAmountShown, () => CloseExchangeAmount());
        EscapeCloses(() => _exRequestPending, () => AnswerExchangeRequest(false));
        EscapeCloses(() => _exWaiting, () => CancelExchangeRequest());
        EscapeCloses(() => _exShown, () => AbortExchange(local: true));
        EscapeCloses(() => _merchantAdvertLayer is { Visible:true }, () => _merchantAdvertLayer.Visible=false);
        EscapeCloses(() => _marketPriceShown, CloseMarketPrice);
        EscapeCloses(() => _amountLayer.Visible, CloseAmountPrompt);
        EscapeCloses(() => _shopShown, () => CloseShop());
        EscapeCloses(() => _wishFindShown, CloseWishFind);
        EscapeCloses(() => _wishShown, CloseWishList);
        EscapeCloses(() => _wantedShown, CloseWantedStall);
        EscapeCloses(() => _sellStallShown, CloseSellStall);
        EscapeCloses(() => _merchantMenuShown, CloseMerchantMenu);
        EscapeCloses(() => _clanCreateShown, () => CloseClanCreate());
        EscapeCloses(() => _clanPointsShown, () => CloseClanPoints());
        EscapeCloses(() => _characterClanDetails is { Visible: true }, () => _characterClanDetails!.Visible = false);
        EscapeCloses(() => _warpShown, () => CloseWarp());
        EscapeCloses(() => _rankShown, () => ToggleRank());
        EscapeCloses(() => _petShown, () => TogglePet());
        EscapeCloses(() => _pusShown && _pusModal != PusModal.None, ClosePusModal);
        EscapeCloses(() => _pusShown, CloseShoppingMall);
        EscapeCloses(() => _rebirthShown, () => CloseRebirth());
        EscapeCloses(() => _admSpawnShown, CloseAdminSpawn);
        EscapeCloses(() => _kingBallotShown, () => PressKingBallot(null));
        EscapeCloses(() => _nationTaxRateShown, CloseNationTaxRate);
        EscapeCloses(() => _nationIntroShown, CloseNationIntro);
        EscapeCloses(() => _nationTaxShown, CloseNationTax);
        EscapeCloses(() => _kingVoteShown, CloseKingVote);
        EscapeCloses(() => _kingPlanShown, CloseKingPlanEditor);
        EscapeCloses(() => _kingNominateShown, CloseKingNominate);
        EscapeCloses(() => _kingElectionShown, CloseKingElection);
        EscapeCloses(() => _siegeTaxRateShown, CloseSiegeTaxRate);
        EscapeCloses(() => _siegeTaxListShown, CloseSiegeTaxList);
        EscapeCloses(() => _siegeOfficeShown, CloseSiegeOffice);
        EscapeCloses(() => _siegeDefendersShown, CloseSiegeDefenders);
        EscapeCloses(() => _siegeChallengersShown, CloseSiegeChallengers);
        EscapeCloses(() => _siegeScheduleShown, CloseSiegeSchedule);
        EscapeCloses(() => _siegeGuardShown, CloseSiegeGuard);
        EscapeCloses(() => _capeShown, () => CloseCape());
        EscapeCloses(() => _clanWhShown, () => CloseClanWarehouse());
        EscapeCloses(() => _vipWhShown, () => CloseVipWarehouse());
        EscapeCloses(() => _reportShown, () => CloseReport());
        EscapeCloses(() => _changeHairShown, () => CloseChangeHair());
        EscapeCloses(() => _achShown, () => CloseAchievements());
        EscapeCloses(() => _mailShown, () => CloseMail());
        EscapeCloses(() => _lotteryShown, () => CloseLottery());
        EscapeCloses(() => _specialAuctionShown, () => CloseSpecialAuction());
        EscapeCloses(() => _attendanceShown, () => CloseAttendance());
        EscapeCloses(() => _bountyShown, () => CloseBounty());
        EscapeCloses(() => _tournamentShown, () => CloseTournament());
        EscapeCloses(() => _disguiseShown, () => CloseDisguise());
        EscapeCloses(() => _presetShown, () => ClosePreset());
        EscapeCloses(() => _titleShown, () => CloseTitlePicker());
        EscapeCloses(() => _msgrShown, () => CloseMessenger());
        EscapeCloses(() => _forcesShown, () => CloseForces());
        EscapeCloses(() => _chatRoomShown, () => CloseChatRoom());
        EscapeCloses(() => _fortuneShown, () => CloseFortune());
        EscapeCloses(() => _combineBookShown, () => CloseCombineRecipeBook());
        EscapeCloses(() => _itemCombineShown, () => CloseItemCombine());
        EscapeCloses(() => _rouletteShown, () => CloseRoulette());
        EscapeCloses(() => _fishHallShown, () => CloseFishingHall());
        EscapeCloses(() => _globalMapShown, () => CloseGlobalMap());
        EscapeCloses(() => _genieShown, () => CloseGenie());
        EscapeCloses(() => _admShown, () => CloseAdminPanel());
        EscapeCloses(() => _mainShown, () => SetMainShown(false));
        EscapeCloses(AnyWhisperExpanded, MinimizeAllWhispers);
    }

    private void HandleEscape()
    {
        if (_deathLayer is { Visible: true }) { ToggleEsc(); return; }

        foreach (var (isOpen, close) in _escapeStack)
            if (isOpen()) { close(); return; }
        ToggleEsc();
    }
}
