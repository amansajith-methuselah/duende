import React, { useState, useEffect } from 'react';
import './AdminDashboard.css';

function AdminDashboard() {
  const [users, setUsers] = useState([]);
  const [tenants, setTenants] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [selectedTenant, setSelectedTenant] = useState('all');
  const [activeTab, setActiveTab] = useState('users');

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

  const fetchData = async () => {
    setLoading(true);
    setError(null);
    
    try {
      // Fetch tenants
      const tenantsResponse = await fetch(`${API_BASE_URL}/api/admin/tenants`, {
        credentials: 'include',
        headers: { 'X-CSRF': '1' }
      });

      if (!tenantsResponse.ok) {
        throw new Error('Failed to fetch tenants');
      }

      const tenantsData = await tenantsResponse.json();
      setTenants(tenantsData.tenants || []);

      // Fetch users (filtered by tenant if selected)
      const usersUrl = selectedTenant === 'all' 
        ? `${API_BASE_URL}/api/admin/users`
        : `${API_BASE_URL}/api/admin/users?tenantId=${selectedTenant}`;

      const usersResponse = await fetch(usersUrl, {
        credentials: 'include',
        headers: { 'X-CSRF': '1' }
      });

      if (!usersResponse.ok) {
        throw new Error('Failed to fetch users');
      }

      const usersData = await usersResponse.json();
      setUsers(usersData.users || []);
    } catch (err) {
      console.error('Error fetching data:', err);
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  const handleLockUser = async (userId, lock) => {
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

      if (!response.ok) {
        throw new Error('Failed to update user lock status');
      }

      // Refresh data
      fetchData();
      alert(`User ${lock ? 'locked' : 'unlocked'} successfully`);
    } catch (err) {
      console.error('Error updating user:', err);
      alert('Failed to update user: ' + err.message);
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
        <h1>🛡️ Admin Dashboard</h1>
        <p>Manage users and tenants across the platform</p>
      </div>

      {error && (
        <div className="error-banner">
          ⚠️ {error}
        </div>
      )}

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
                <button 
                  onClick={() => setSelectedTenant(tenant.tenantId)}
                  className="btn-view-users"
                >
                  View Users
                </button>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}

export default AdminDashboard;