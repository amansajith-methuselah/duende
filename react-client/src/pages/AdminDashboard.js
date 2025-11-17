import React, { useState, useEffect } from 'react';
import './AdminDashboard.css';

/* eslint-disable no-restricted-globals */

function AdminDashboard() {
  const [users, setUsers] = useState([]);
  const [tenants, setTenants] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [selectedTenant, setSelectedTenant] = useState('all');
  const [activeTab, setActiveTab] = useState('users');
  const [showAddTenantModal, setShowAddTenantModal] = useState(false);
  const [newTenant, setNewTenant] = useState({
    tenantId: '',
    name: '',
    domain: '',
    primaryColor: '#059669',
    useOwnDatabase: false,
    customConnectionString: ''
  });

  // Database management state
  const [selectedDbTenant, setSelectedDbTenant] = useState(null);
  const [dbConfig, setDbConfig] = useState(null);
  const [loadingDbConfig, setLoadingDbConfig] = useState(false);
  const [testingConnection, setTestingConnection] = useState(false);
  const [migrating, setMigrating] = useState(false);
  const [connectionString, setConnectionString] = useState('');
  const [useOwnDatabase, setUseOwnDatabase] = useState(false);

  const getApiBaseUrl = () => {
    const host = window.location.host;
    const port = window.location.port;
    const bffHost = host.replace(`:${port}`, ':5001');
    return `https://${bffHost}`;
  };

  const API_BASE_URL = getApiBaseUrl();

  useEffect(() => {
    fetchData();
  }, [selectedTenant]);

  useEffect(() => {
    if (selectedDbTenant) {
      fetchDatabaseConfig(selectedDbTenant);
    }
  }, [selectedDbTenant]);

  const fetchData = async () => {
    setLoading(true);
    setError(null);
    
    try {
      const tenantsResponse = await fetch(`${API_BASE_URL}/api/admin/tenants`, {
        credentials: 'include',
        headers: { 'X-CSRF': '1' }
      });

      if (!tenantsResponse.ok) throw new Error('Failed to fetch tenants');
      const tenantsData = await tenantsResponse.json();
      setTenants(tenantsData.tenants || []);

      const usersUrl = selectedTenant === 'all' 
        ? `${API_BASE_URL}/api/admin/users`
        : `${API_BASE_URL}/api/admin/users?tenantId=${selectedTenant}`;

      const usersResponse = await fetch(usersUrl, {
        credentials: 'include',
        headers: { 'X-CSRF': '1' }
      });

      if (!usersResponse.ok) throw new Error('Failed to fetch users');
      const usersData = await usersResponse.json();
      setUsers(usersData.users || []);
    } catch (err) {
      console.error('Error fetching data:', err);
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  const fetchDatabaseConfig = async (tenantId) => {
    setLoadingDbConfig(true);
    try {
      const response = await fetch(`${API_BASE_URL}/api/admin/tenants/${tenantId}/database-config`, {
        credentials: 'include',
        headers: { 'X-CSRF': '1' }
      });

      if (!response.ok) throw new Error('Failed to fetch database config');
      const data = await response.json();
      setDbConfig(data.config);
      setUseOwnDatabase(data.config.useOwnDatabase);
      setConnectionString('');
    } catch (err) {
      console.error('Error fetching database config:', err);
      alert('Failed to fetch database configuration: ' + err.message);
    } finally {
      setLoadingDbConfig(false);
    }
  };

  const handleTestConnection = async () => {
    if (!connectionString) {
      alert('Please enter a connection string');
      return;
    }

    setTestingConnection(true);
    try {
      const response = await fetch(`${API_BASE_URL}/api/admin/tenants/${selectedDbTenant}/database-config/test`, {
        method: 'POST',
        credentials: 'include',
        headers: {
          'Content-Type': 'application/json',
          'X-CSRF': '1'
        },
        body: JSON.stringify({ connectionString })
      });

      const data = await response.json();
      
      if (data.success) {
        alert(`✅ Connection successful!\n\nDatabase: ${data.databaseName}\nServer: ${data.serverVersion}`);
      } else {
        alert(`❌ Connection failed:\n\n${data.error}`);
      }
    } catch (err) {
      console.error('Error testing connection:', err);
      alert('Failed to test connection: ' + err.message);
    } finally {
      setTestingConnection(false);
    }
  };

  const handleSaveDatabaseConfig = async () => {
    if (useOwnDatabase && !connectionString) {
      alert('Please enter a connection string or disable "Use Own Database"');
      return;
    }

    if (!confirm('Are you sure you want to update the database configuration?')) return;

    try {
      const response = await fetch(`${API_BASE_URL}/api/admin/tenants/${selectedDbTenant}/database-config`, {
        method: 'PUT',
        credentials: 'include',
        headers: {
          'Content-Type': 'application/json',
          'X-CSRF': '1'
        },
        body: JSON.stringify({
          useOwnDatabase,
          customConnectionString: useOwnDatabase ? connectionString : null
        })
      });

      if (!response.ok) {
        const errorData = await response.json();
        throw new Error(errorData.error || 'Failed to update configuration');
      }

      const data = await response.json();
      alert('✅ Database configuration updated successfully!');
      
      if (data.requiresMigration) {
        if (confirm('The database needs migration. Run migrations now?')) {
          await handleRunMigrations();
        }
      }
      
      fetchDatabaseConfig(selectedDbTenant);
    } catch (err) {
      console.error('Error saving database config:', err);
      alert('Failed to save configuration: ' + err.message);
    }
  };

  const handleRunMigrations = async () => {
    if (!confirm(`Run database migrations for ${selectedDbTenant}?\n\nThis will create/update database tables in the tenant's database.`)) return;

    setMigrating(true);
    try {
      const response = await fetch(`${API_BASE_URL}/api/admin/tenants/${selectedDbTenant}/database-config/migrate`, {
        method: 'POST',
        credentials: 'include',
        headers: { 'X-CSRF': '1' }
      });

      const data = await response.json();
      
      if (data.success) {
        alert(`✅ Migrations completed successfully!\n\n${data.message}`);
        fetchDatabaseConfig(selectedDbTenant);
      } else {
        alert(`❌ Migration failed:\n\n${data.error}`);
      }
    } catch (err) {
      console.error('Error running migrations:', err);
      alert('Failed to run migrations: ' + err.message);
    } finally {
      setMigrating(false);
    }
  };

  const handleLockUser = async (userId, lock) => {
    if (!confirm(`Are you sure you want to ${lock ? 'lock' : 'unlock'} this user?`)) return;

    try {
      const response = await fetch(`${API_BASE_URL}/api/admin/users/${userId}/lock`, {
        method: 'POST',
        credentials: 'include',
        headers: {
          'Content-Type': 'application/json',
          'X-CSRF': '1'
        },
        body: JSON.stringify({ lock })
      });

      if (!response.ok) throw new Error('Failed to update user lock status');
      fetchData();
      alert(`User ${lock ? 'locked' : 'unlocked'} successfully`);
    } catch (err) {
      console.error('Error updating user:', err);
      alert('Failed to update user: ' + err.message);
    }
  };

  const handleDeleteUser = async (userId, userName) => {
    if (!confirm(`Are you sure you want to DELETE user "${userName}"? This action cannot be undone!`)) return;

    try {
      const response = await fetch(`${API_BASE_URL}/api/admin/users/${userId}`, {
        method: 'DELETE',
        credentials: 'include',
        headers: { 'X-CSRF': '1' }
      });

      if (!response.ok) {
        const errorData = await response.json();
        throw new Error(errorData.error || 'Failed to delete user');
      }

      fetchData();
      alert('User deleted successfully');
    } catch (err) {
      console.error('Error deleting user:', err);
      alert('Failed to delete user: ' + err.message);
    }
  };

  const handleDeleteTenant = async (tenantId, tenantName) => {
    if (!confirm(`Are you sure you want to DELETE tenant "${tenantName}"? This will also delete the tenant's client configuration!`)) return;

    try {
      const response = await fetch(`${API_BASE_URL}/api/admin/tenants/${tenantId}`, {
        method: 'DELETE',
        credentials: 'include',
        headers: { 'X-CSRF': '1' }
      });

      if (!response.ok) {
        const errorData = await response.json();
        throw new Error(errorData.error || 'Failed to delete tenant');
      }

      fetchData();
      setSelectedTenant('all');
      alert('Tenant deleted successfully');
    } catch (err) {
      console.error('Error deleting tenant:', err);
      alert('Failed to delete tenant: ' + err.message);
    }
  };

  const handleAddTenant = async (e) => {
    e.preventDefault();

    if (!newTenant.tenantId || !newTenant.name || !newTenant.domain) {
      alert('Please fill in all required fields');
      return;
    }

    if (newTenant.useOwnDatabase && !newTenant.customConnectionString) {
      alert('Please provide a connection string for dedicated database or uncheck the option');
      return;
    }

    try {
      // First, create the tenant
      const createResponse = await fetch(`${API_BASE_URL}/api/admin/tenants`, {
        method: 'POST',
        credentials: 'include',
        headers: {
          'Content-Type': 'application/json',
          'X-CSRF': '1'
        },
        body: JSON.stringify({
          tenantId: newTenant.tenantId,
          name: newTenant.name,
          domain: newTenant.domain,
          primaryColor: newTenant.primaryColor
        })
      });

      if (!createResponse.ok) {
        const errorData = await createResponse.json();
        throw new Error(errorData.error || 'Failed to create tenant');
      }

      // If database configuration is specified, update it
      if (newTenant.useOwnDatabase && newTenant.customConnectionString) {
        const dbConfigResponse = await fetch(`${API_BASE_URL}/api/admin/tenants/${newTenant.tenantId}/database-config`, {
          method: 'PUT',
          credentials: 'include',
          headers: {
            'Content-Type': 'application/json',
            'X-CSRF': '1'
          },
          body: JSON.stringify({
            useOwnDatabase: true,
            customConnectionString: newTenant.customConnectionString
          })
        });

        if (!dbConfigResponse.ok) {
          console.error('Failed to configure database, but tenant was created');
        }
      }

      fetchData();
      setShowAddTenantModal(false);
      setNewTenant({ 
        tenantId: '', 
        name: '', 
        domain: '', 
        primaryColor: '#059669',
        useOwnDatabase: false,
        customConnectionString: ''
      });
      
      let successMessage = 'Tenant created successfully! Remember to add it to your hosts file.';
      if (newTenant.useOwnDatabase) {
        successMessage += '\n\nDatabase configured. You may need to run migrations from the Database Config tab.';
      }
      alert(successMessage);
    } catch (err) {
      console.error('Error creating tenant:', err);
      alert('Failed to create tenant: ' + err.message);
    }
  };

  const filteredUsers = users.filter(user => 
    selectedTenant === 'all' || user.tenantId === selectedTenant
  );

  if (loading) {
    return (
      <div className="admin-dashboard">
        <div className="loading">
          <div className="spinner"></div>
          <p>Loading admin dashboard...</p>
        </div>
      </div>
    );
  }

  return (
    <div className="admin-dashboard">
      <div className="admin-header">
        <div className="header-top">
          <div>
            <h1>🛡️ Admin Dashboard</h1>
            <p>Manage users and tenants across the platform</p>
          </div>
          <a href="/" className="btn-back-home">← Back to Home</a>
        </div>
      </div>

      {error && <div className="error-banner">⚠️ {error}</div>}

      <div className="admin-tabs">
        <button 
          className={`tab ${activeTab === 'users' ? 'active' : ''}`}
          onClick={() => setActiveTab('users')}
        >
          👥 Users ({filteredUsers.length})
        </button>
        <button 
          className={`tab ${activeTab === 'tenants' ? 'active' : ''}`}
          onClick={() => setActiveTab('tenants')}
        >
          🏢 Tenants ({tenants.length})
        </button>
        <button 
          className={`tab ${activeTab === 'database' ? 'active' : ''}`}
          onClick={() => setActiveTab('database')}
        >
          💾 Database Config
        </button>
      </div>

      {activeTab === 'users' && (
        <div className="users-section">
          <div className="section-header">
            <h2>User Management</h2>
            <div className="filters">
              <label>Filter by Tenant:</label>
              <select 
                value={selectedTenant} 
                onChange={(e) => setSelectedTenant(e.target.value)}
                className="tenant-filter"
              >
                <option value="all">All Tenants</option>
                {tenants.map(tenant => (
                  <option key={tenant.tenantId} value={tenant.tenantId}>
                    {tenant.name}
                  </option>
                ))}
              </select>
            </div>
          </div>

          <div className="users-table-container">
            <table className="users-table">
              <thead>
                <tr>
                  <th>Username</th>
                  <th>Email</th>
                  <th>Phone</th>
                  <th>Tenant</th>
                  <th>Roles</th>
                  <th>Status</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {filteredUsers.map(user => (
                  <tr key={user.id}>
                    <td>{user.userName}</td>
                    <td>{user.email}</td>
                    <td>{user.phoneNumber || '-'}</td>
                    <td>
                      <span className="tenant-badge">
                        {user.tenantId || 'No Tenant'}
                      </span>
                    </td>
                    <td>
                      {user.roles && user.roles[0] ? (
                        <span className="role-badge">{user.roles.join(', ')}</span>
                      ) : (
                        <span className="no-role">-</span>
                      )}
                    </td>
                    <td>
                      {user.isLockedOut ? (
                        <span className="status-locked">🔒 Locked</span>
                      ) : (
                        <span className="status-active">✅ Active</span>
                      )}
                    </td>
                    <td>
                      <div className="action-buttons-cell">
                        {user.isLockedOut ? (
                          <button 
                            onClick={() => handleLockUser(user.id, false)}
                            className="btn-unlock"
                          >
                            Unlock
                          </button>
                        ) : (
                          <button 
                            onClick={() => handleLockUser(user.id, true)}
                            className="btn-lock"
                          >
                            Lock
                          </button>
                        )}
                        <button 
                          onClick={() => handleDeleteUser(user.id, user.userName)}
                          className="btn-delete"
                        >
                          Delete
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {activeTab === 'tenants' && (
        <div className="tenants-section">
          <div className="section-header">
            <h2>Tenant Overview</h2>
            <button 
              onClick={() => setShowAddTenantModal(true)}
              className="btn-add-tenant"
            >
              + Add New Tenant
            </button>
          </div>

          <div className="tenants-grid">
            {tenants.map(tenant => (
              <div key={tenant.tenantId} className="tenant-card">
                <div className="tenant-header">
                  <h3>{tenant.name}</h3>
                  <span className={`status-indicator ${tenant.isActive ? 'active' : 'inactive'}`}>
                    {tenant.isActive ? '● Active' : '○ Inactive'}
                  </span>
                </div>
                <div className="tenant-details">
                  <div className="detail-row">
                    <span className="label">Tenant ID:</span>
                    <span className="value">{tenant.tenantId}</span>
                  </div>
                  <div className="detail-row">
                    <span className="label">Domain:</span>
                    <span className="value">{tenant.domain}</span>
                  </div>
                  <div className="detail-row">
                    <span className="label">Users:</span>
                    <span className="value">{tenant.userCount}</span>
                  </div>
                </div>
                <div className="tenant-actions">
                  <button 
                    onClick={() => {
                      setSelectedTenant(tenant.tenantId);
                      setActiveTab('users');
                    }}
                    className="btn-view-users"
                  >
                    View Users
                  </button>
                  {tenant.tenantId !== 'admin' && (
                    <button 
                      onClick={() => handleDeleteTenant(tenant.tenantId, tenant.name)}
                      className="btn-delete-tenant"
                    >
                      Delete
                    </button>
                  )}
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {activeTab === 'database' && (
        <div className="database-section">
          <div className="section-header">
            <h2>💾 Database Configuration</h2>
          </div>

          <div className="db-tenant-selector">
            <label>Select Tenant to Configure:</label>
            <select 
              value={selectedDbTenant || ''} 
              onChange={(e) => setSelectedDbTenant(e.target.value)}
              className="tenant-filter"
            >
              <option value="">-- Select a Tenant --</option>
              {tenants.filter(t => t.tenantId !== 'admin').map(tenant => (
                <option key={tenant.tenantId} value={tenant.tenantId}>
                  {tenant.name} ({tenant.tenantId})
                </option>
              ))}
            </select>
          </div>

          {selectedDbTenant && (
            <div className="db-config-panel">
              {loadingDbConfig ? (
                <div className="loading-small">
                  <div className="spinner-small"></div>
                  <p>Loading configuration...</p>
                </div>
              ) : dbConfig ? (
                <>
                  <div className="db-current-status">
                    <h3>Current Configuration</h3>
                    <div className="status-grid">
                      <div className="status-item">
                        <span className="status-label">Database Mode:</span>
                        <span className="status-value">
                          {dbConfig.useOwnDatabase ? '🔹 Own Database' : '🔸 Shared Database'}
                        </span>
                      </div>
                      <div className="status-item">
                        <span className="status-label">Migration Status:</span>
                        <span className={`status-badge status-${(dbConfig.databaseMigrationStatus || 'notconfigured').toLowerCase()}`}>
                          {dbConfig.databaseMigrationStatus || 'Not Configured'}
                        </span>
                      </div>
                      <div className="status-item">
                        <span className="status-label">Last Migration:</span>
                        <span className="status-value">
                          {dbConfig.lastMigrationDate 
                            ? new Date(dbConfig.lastMigrationDate).toLocaleString()
                            : 'Never'}
                        </span>
                      </div>
                      <div className="status-item">
                        <span className="status-label">Global Multi-DB Mode:</span>
                        <span className="status-value">
                          {dbConfig.globalMultiDbMode ? '✅ Enabled' : '❌ Disabled'}
                        </span>
                      </div>
                    </div>
                  </div>

                  <div className="db-configuration-form">
                    <h3>Update Configuration</h3>
                    
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox"
                          checked={useOwnDatabase}
                          onChange={(e) => setUseOwnDatabase(e.target.checked)}
                        />
                        <span>Use dedicated database for this tenant</span>
                      </label>
                      <small>When enabled, this tenant will use its own database instead of the shared one.</small>
                    </div>

                    {useOwnDatabase && (
                      <>
                        <div className="form-group">
                          <label>Custom Connection String *</label>
                          <textarea
                            value={connectionString}
                            onChange={(e) => setConnectionString(e.target.value)}
                            placeholder="Server=localhost\SQLEXPRESS;Database=TenantDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;Encrypt=True"
                            rows="4"
                            className="connection-string-input"
                          />
                          <small>Enter the SQL Server connection string for this tenant's database.</small>
                        </div>

                        <div className="db-actions">
                          <button 
                            onClick={handleTestConnection}
                            disabled={testingConnection || !connectionString}
                            className="btn-test-connection"
                          >
                            {testingConnection ? '🔄 Testing...' : '🔌 Test Connection'}
                          </button>
                        </div>
                      </>
                    )}

                    <div className="db-actions">
                      <button 
                        onClick={handleSaveDatabaseConfig}
                        className="btn-save-config"
                      >
                        💾 Save Configuration
                      </button>
                      
                      {dbConfig.useOwnDatabase && dbConfig.hasCustomConnection && (
                        <button 
                          onClick={handleRunMigrations}
                          disabled={migrating}
                          className="btn-run-migrations"
                        >
                          {migrating ? '🔄 Running Migrations...' : '🚀 Run Migrations'}
                        </button>
                      )}
                    </div>
                  </div>

                  <div className="db-info-box">
                    <h4>ℹ️ Important Information</h4>
                    <ul>
                      <li>Changes to database configuration require running migrations before users can authenticate</li>
                      <li>Test the connection before saving to avoid configuration errors</li>
                      <li>Migration status will update automatically after running migrations</li>
                      <li>Switching from shared to dedicated database does NOT migrate existing user data</li>
                    </ul>
                  </div>
                </>
              ) : (
                <div className="no-config">No configuration found for this tenant</div>
              )}
            </div>
          )}
        </div>
      )}

      {showAddTenantModal && (
        <div className="modal-overlay" onClick={() => setShowAddTenantModal(false)}>
          <div className="modal-content modal-large" onClick={(e) => e.stopPropagation()}>
            <div className="modal-header">
              <h2>Add New Tenant</h2>
              <button onClick={() => setShowAddTenantModal(false)} className="modal-close">×</button>
            </div>
            <form onSubmit={handleAddTenant} className="tenant-form">
              <div className="form-section">
                <h3>Basic Information</h3>
                <div className="form-group">
                  <label>Tenant ID *</label>
                  <input
                    type="text"
                    value={newTenant.tenantId}
                    onChange={(e) => setNewTenant({...newTenant, tenantId: e.target.value})}
                    placeholder="e.g., tenant4"
                    required
                  />
                  <small>Lowercase, no spaces. Used in subdomain.</small>
                </div>
                <div className="form-group">
                  <label>Tenant Name *</label>
                  <input
                    type="text"
                    value={newTenant.name}
                    onChange={(e) => setNewTenant({...newTenant, name: e.target.value})}
                    placeholder="e.g., Tenant Four Corporation"
                    required
                  />
                </div>
                <div className="form-group">
                  <label>Domain *</label>
                  <input
                    type="text"
                    value={newTenant.domain}
                    onChange={(e) => setNewTenant({...newTenant, domain: e.target.value})}
                    placeholder="e.g., tenant4.localhost:7140"
                    required
                  />
                  <small>Format: tenantId.localhost:7140</small>
                </div>
                <div className="form-group">
                  <label>Primary Color</label>
                  <input
                    type="color"
                    value={newTenant.primaryColor}
                    onChange={(e) => setNewTenant({...newTenant, primaryColor: e.target.value})}
                  />
                </div>
              </div>

              <div className="form-section">
                <h3>Database Configuration</h3>
                <div className="form-group">
                  <label className="checkbox-label">
                    <input 
                      type="checkbox"
                      checked={newTenant.useOwnDatabase}
                      onChange={(e) => setNewTenant({...newTenant, useOwnDatabase: e.target.checked})}
                    />
                    <span>Use dedicated database for this tenant</span>
                  </label>
                  <small>When enabled, this tenant will use its own database instead of the shared one.</small>
                </div>

                {newTenant.useOwnDatabase && (
                  <div className="form-group">
                    <label>Custom Connection String *</label>
                    <textarea
                      value={newTenant.customConnectionString}
                      onChange={(e) => setNewTenant({...newTenant, customConnectionString: e.target.value})}
                      placeholder="Server=localhost\SQLEXPRESS;Database=TenantDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;Encrypt=True"
                      rows="4"
                      className="connection-string-input"
                      required={newTenant.useOwnDatabase}
                    />
                    <small>Enter the SQL Server connection string for this tenant's database. You'll need to run migrations after creation.</small>
                  </div>
                )}
              </div>

              <div className="modal-actions">
                <button type="button" onClick={() => setShowAddTenantModal(false)} className="btn-cancel">
                  Cancel
                </button>
                <button type="submit" className="btn-submit">
                  Create Tenant
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}

export default AdminDashboard;