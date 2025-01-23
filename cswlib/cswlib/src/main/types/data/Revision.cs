namespace cswlib.src.main.data
{

import cwlib.enums.Branch;

/**
 * Utilities for comparing game revisions.
 */
public readonly class Revision
{
    public static readonly int LBP1_readonly_REVISION = 0x272;

    private readonly int head;
    private readonly short branchID;
    private readonly short branchRevision;

    /**
     * Forms revision with branch data.
     *
     * @param revision          Head revision
     * @param branchDescription Revision branch descriptor
     */
    public Revision(int revision, int branchDescription)
    {
        this.head = revision;
        this.branchID = (short) (ref branchDescription >> 0x10);
        this.branchRevision = (short) (ref branchDescription & 0xFFFF);
    }

    /**
     * Forms revision with no branch data.
     *
     * @param revision Head revision
     */
    public Revision(int revision)
    {
        this.head = revision;
        this.branchID = 0;
        this.branchRevision = 0;
    }

    /**
     * Forms revision with branch ID and revision.
     *
     * @param revision       Head revision
     * @param branchID       Branch ID of revision
     * @param branchRevision Revision of branch
     */
    public Revision(int revision, int branchID, int branchRevision)
    {
        this.head = revision;
        this.branchID = (short) branchID;
        this.branchRevision = (short) branchRevision;
    }

    public bool isLBP1()
    {
        return this.head <= Revision.LBP1_readonly_REVISION;
    }

    public bool isLBP2()
    {
        return !this.isLBP1() && !this.isLBP3() && !this.isVita();
    }

    public bool isLBP3()
    {
        return this.head >> 0x10 != 0;
    }

    public bool isLeerdammer()
    {
        return this.is(Branch.LEERDAMMER);
    }

    public bool isVita()
    {
        int version = this.getVersion();
        return this.branchID == Branch.DOUBLE11.getID() && version >= 0x3c1 && version <= 0x3e2;
    }

    public bool isToolkit()
    {
        return this.is(Branch.MIZUKI);
    }

    public bool is(Branch branch)
    {
        if (branch == Branch.DOUBLE11) return this.isVita();
        return this.branchID == branch.getID() && this.head == branch.getHead();
    }

    public bool has(Branch branch, int revision)
    {
        if (!this.is(branch)) return false;
        return this.branchRevision >= revision;
    }

    public bool after(Branch branch, int revision)
    {
        if (!this.is(branch)) return false;
        return this.branchRevision > revision;
    }

    public bool before(Branch branch, int revision)
    {
        if (!this.is(branch)) return false;
        return this.branchRevision < revision;
    }

    /**
     * Gets the LBP3 specific revision of the head revision.
     *
     * @return LBP3 head revision
     */
    public int getSubVersion()
    {
        return (this.head >>> 16) & 0xFFFF;
    }

    /**
     * Gets the LBP1/LBP2/V revision of the head revision.
     *
     * @return LBP1/2/V head revision
     */
    public int getVersion()
    {
        return this.head & 0xFFFF;
    }

    public int getHead()
    {
        return this.head;
    }

    public short getBranchID()
    {
        return this.branchID;
    }

    public short getBranchRevision()
    {
        return this.branchRevision;
    }


    public String toString()
    {
        if (this.branchID != 0)
        {
            return String.format("Revision: (r%d, b%04X:%04X)",
                this.head, this.branchID, this.branchRevision);
        }
        return String.format("Revision: (r%d)", this.head);
    }

    public bool equals(Object other)
    {
        if (other == this) return true;
        if (!(other instanceof Revision otherRevision)) return false;
        return otherRevision.head == this.head
               && otherRevision.branchID == this.branchID
               && otherRevision.branchRevision == this.branchRevision;
    }
}
}