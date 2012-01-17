using System;

namespace ExternalLibrary {
    public class Db {
        public void LoadTables()
        {
            throw new InvalidOperationException("Invalid Database");
        }

        public void LoadTablesAndConnect()
        {
            try {
                Connect();
            }
            catch (Exception e) {
                throw new InvalidOperationException("Invalid Database", e);
            }
        }

        private void Connect()
        {
            throw new UnauthorizedAccessException("Could not connect");
        }
    }
}
