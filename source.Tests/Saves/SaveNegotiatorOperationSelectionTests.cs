using Graviton.Models.RomM;
using Graviton.Models.RomM.Saves;
using Graviton.Saves;

using Xunit;

namespace Graviton.Tests.Saves;

public sealed class SaveNegotiatorOperationSelectionTests
{
    [Fact]
    public void FindOperationForSave_ReturnsMatchingROMAndSlot()
    {
        var differentRom = new RomMNegotiateOperations
        {
            ROMID = 13,
            Slot = "autosave",
            Action = SaveSyncStatus.download
        };
        var differentSlot = new RomMNegotiateOperations
        {
            ROMID = 42,
            Slot = "quicksave",
            Action = SaveSyncStatus.conflict
        };
        var expected = new RomMNegotiateOperations
        {
            ROMID = 42,
            Slot = "autosave",
            Action = SaveSyncStatus.upload
        };
        var operations = new List<RomMNegotiateOperations>
        {
            differentRom,
            differentSlot,
            expected
        };

        var result = SaveNegotiator.FindOperationForSave(operations, 42, "autosave");

        Assert.Same(expected, result);
    }

    [Fact]
    public void FindOperationForSave_WithUnknownROM_ReturnsNull()
    {
        var operations = new List<RomMNegotiateOperations>
        {
            new()
            {
                ROMID = 42,
                Slot = "autosave"
            }
        };

        var result = SaveNegotiator.FindOperationForSave(operations, 99, "autosave");

        Assert.Null(result);
    }

    [Fact]
    public void FindOperationForSave_WithUnknownSlot_ReturnsNull()
    {
        var operations = new List<RomMNegotiateOperations>
        {
            new()
            {
                ROMID = 42,
                Slot = "autosave"
            }
        };

        var result = SaveNegotiator.FindOperationForSave(operations, 42, "quicksave");

        Assert.Null(result);
    }

    [Fact]
    public void FindOperationForSave_WithNullSlot_ReturnsArchivalOperation()
    {
        var slotted = new RomMNegotiateOperations
        {
            ROMID = 42,
            Slot = "autosave",
            Action = SaveSyncStatus.no_op
        };
        var archival = new RomMNegotiateOperations
        {
            ROMID = 42,
            Slot = null,
            Action = SaveSyncStatus.upload
        };
        var operations = new List<RomMNegotiateOperations> { slotted, archival };

        var result = SaveNegotiator.FindOperationForSave(operations, 42, null);

        Assert.Same(archival, result);
    }

    [Fact]
    public void FindOperationForSave_WithOnlyArchivalOperation_DoesNotMatchNamedSlot()
    {
        var operations = new List<RomMNegotiateOperations>
        {
            new()
            {
                ROMID = 42,
                Slot = null,
                Action = SaveSyncStatus.upload
            }
        };

        var result = SaveNegotiator.FindOperationForSave(operations, 42, "autosave");

        Assert.Null(result);
    }

    [Fact]
    public void FindOperationForSave_WithNoOperations_ReturnsNull()
    {
        var operations = new List<RomMNegotiateOperations>();

        var result = SaveNegotiator.FindOperationForSave(operations, 42, "autosave");

        Assert.Null(result);
    }
}
