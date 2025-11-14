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
    primaryColor: '#059669'
  });

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

    try {
      const response = await fetch(`${API_BASE_URL}/api/admin/tenants`, {
        method: 'POST',
        credentials: 'include',
        headers: {
          'Content-Type': 'application/json',
          'X-CSRF': '1'
        },
        body: JSON.stringify(newTenant)
      });

      if (!response.ok) {
        const errorData = await response.json();
        throw new Error(errorData.error || 'Failed to create tenant');
      }

      fetchData();
      setShowAddTenantModal(false);
      setNewTenant({ tenantId: '', name: '', domain: '', primaryColor: '#059669' });
      alert('Tenant created successfully! Remember to add it to your hosts file.');
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

      {showAddTenantModal && (
        <div className="modal-overlay" onClick={() => setShowAddTenantModal(false)}>
          <div className="modal-content" onClick={(e) => e.stopPropagation()}>
            <div className="modal-header">
              <h2>Add New Tenant</h2>
              <button onClick={() => setShowAddTenantModal(false)} className="modal-close">×</button>
            </div>
            <form onSubmit={handleAddTenant} className="tenant-form">
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