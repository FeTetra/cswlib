using System.Runtime.ConstrainedExecution;

namespace cswlib.src.main.enums
{

    public class BranchDetails
    {
        private readonly int head;
        private readonly short id;
        private readonly short revision;

        public BranchDetails(int head, short id, short revision)
        {
            this.head = head;
            this.id = id;
            this.revision = revision;
        }
    }
    public static class Branch
    {
        public static readonly BranchDetails NONE = new BranchDetails(0x0, 0x0, 0x0);
        public static readonly BranchDetails LEERDAMMER = new BranchDetails(Revisions.LD_HEAD, 0x4c44, Revisions.LD_MAX);
    }
}