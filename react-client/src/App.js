import React, { useState, useEffect } from 'react';
import { BrowserRouter as Router, Routes, Route, Navigate } from 'react-router-dom';
import AdminDashboard from './pages/AdminDashboard';
import './App.css';

function HomePage() {
  const [user, setUser] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [tenantInfo, setTenantInfo] = useState(null);

  // Get tenant-aware API Base URL
  const getApiBaseUrl = () => {
    const host = window.location.host;
    const port = window.location.port;
    const bffHost = host.replace(`:${port}`, ':5001');
    return `https://${bffHost}`;
  };

  const API_BASE_URL = getApiBaseUrl();

  // Extract tenant info from hostname
  useEffect(() => {
    const host = window.location.hostname;
    const parts = host.split('.');
    
    if (parts.length >= 2 && parts[0] !== 'localhost') {
      const tenantId = parts[0];
      setTenantInfo({
        id: tenantId,
        name: tenantId.charAt(0).toUpperCase() + tenantId.slice(1)
      });
    } else {
      setTenantInfo({
        id: 'default',
        name: 'Default'
      });
    }
  }, []);

  const checkAuthentication = React.useCallback(async () => {
    try {
      setLoading(true);
      const response = await fetch(`${API_BASE_URL}/bff/user`, {
        credentials: 'include',
        headers: {
          'X-CSRF': '1'
        }
      });

      if (response.ok) {
        const data = await response.json();
        console.log('User claims:', data);
        setUser(data);
      } else {
        setUser(null);
      }
    } catch (err) {
      console.error('Error checking authentication:', err);
      setError('Failed to check authentication');
      setUser(null);
    } finally {
      setLoading(false);
    }
  }, [API_BASE_URL]);

  useEffect(() => {
    checkAuthentication();
  }, [checkAuthentication]);

  const handleLogin = () => {
    window.location.href = `${API_BASE_URL}/bff/login`;
  };

  const handleLogout = () => {
    window.location.href = `${API_BASE_URL}/bff/logout`;
  };

  const testApi = async () => {
    try {
      const response = await fetch(`${API_BASE_URL}/api/test`, {
        credentials: 'include',
        headers: {
          'X-CSRF': '1'
        }
      });

      if (response.ok) {
        const data = await response.json();
        console.log('API Test Response:', data);
        alert(`API Test Success! Message: ${JSON.stringify(data)}`);
      } else {
        alert('API test failed. Please login again.');
      }
    } catch (err) {
      console.error('Error testing API:', err);
      alert('Error testing API');
    }
  };

  if (loading) {
    return (
      <div className="App">
        <div className="loading">
          <div className="spinner"></div>
          <p>Loading...</p>
        </div>
      </div>
    );
  }

  const isAuthenticated = user && user.length > 0;
  const userName = isAuthenticated ? (user.find(c => c.type === 'name')?.value || 'User') : null;
  const userEmail = isAuthenticated ? (user.find(c => c.type === 'email')?.value || '') : null;
  const isAdmin = isAuthenticated && user.some(c => c.type === 'role' && c.value === 'Admin');

  return (
    <div className="App">
      <header className="App-header">
        <div className="header-content">
          <h1>🔐 React BFF Demo - {tenantInfo?.name}</h1>
          <div className="tenant-badge">
            Tenant: {tenantInfo?.id}
          </div>
          {isAuthenticated ? (
            <div className="user-info">
              <span className="welcome-text">
                Welcome, <strong>{userName}</strong>!
                {isAdmin && <span className="admin-badge">👑 Admin</span>}
              </span>
              <button onClick={handleLogout} className="btn btn-logout">
                Logout
              </button>
            </div>
          ) : (
            <button onClick={handleLogin} className="btn btn-login">
              Login
            </button>
          )}
        </div>
      </header>

      <main className="App-main">
        {error && (
          <div className="error-message">
            <p>⚠️ {error}</p>
          </div>
        )}

        {isAuthenticated ? (
          <div className="authenticated-content">
            <div className="welcome-card">
              <h2>🎉 You are logged in to {tenantInfo?.name}!</h2>
              <p>Welcome to the multi-tenant React application with BFF authentication pattern.</p>
            </div>

            {isAdmin && (
              <div className="admin-access-card">
                <h3>🛡️ Admin Access</h3>
                <p>You have administrative privileges. Access the admin dashboard to manage users and tenants.</p>
                <a href="/admin" className="btn btn-admin-dashboard">
                  Go to Admin Dashboard →
                </a>
              </div>
            )}

            <div className="user-details-card">
              <h3>📋 User Information</h3>
              <div className="user-details">
                <div className="detail-item">
                  <span className="label">Name:</span>
                  <span className="value">{userName || 'N/A'}</span>
                </div>
                <div className="detail-item">
                  <span className="label">Email:</span>
                  <span className="value">{userEmail || 'N/A'}</span>
                </div>
                <div className="detail-item">
                  <span className="label">Tenant:</span>
                  <span className="value">{tenantInfo?.id}</span>
                </div>
              </div>
            </div>

            <div className="actions-card">
              <h3>🚀 Available Actions</h3>
              <div className="action-buttons">
                <button onClick={testApi} className="btn btn-action">
                  Test API Endpoint
                </button>
                <button onClick={() => console.log('User claims:', user)} className="btn btn-action">
                  View Claims in Console
                </button>
              </div>
            </div>

            {user && (
              <div className="claims-card">
                <h3>🔑 User Claims</h3>
                <div className="claims-list">
                  {user.map((claim, index) => (
                    <div key={index} className="claim-item">
                      <span className="claim-key">{claim.type}:</span>
                      <span className="claim-value">{claim.value}</span>
                    </div>
                  ))}
                </div>
              </div>
            )}
          </div>
        ) : (
          <div className="unauthenticated-content">
            <div className="login-card">
              <div className="lock-icon">🔒</div>
              <h2>Welcome to {tenantInfo?.name} Portal</h2>
              <p>This application demonstrates multi-tenant BFF pattern with:</p>
              <ul className="features-list">
                <li>✅ Tenant-specific IdentityServer</li>
                <li>✅ Duende IdentityServer authentication</li>
                <li>✅ ASP.NET Core BFF middleware</li>
                <li>✅ React frontend with secure API calls</li>
                <li>✅ Cookie-based authentication</li>
                <li>✅ Complete tenant data isolation</li>
              </ul>
              <button onClick={handleLogin} className="btn btn-primary btn-large">
                🔓 Login to Get Started
              </button>
            </div>

            <div className="info-card">
              <h3>📚 How It Works</h3>
              <div className="info-steps">
                <div className="step">
                  <span className="step-number">1</span>
                  <div className="step-content">
                    <h4>Click Login</h4>
                    <p>You'll be redirected to {tenantInfo?.name}'s Identity Server</p>
                  </div>
                </div>
                <div className="step">
                  <span className="step-number">2</span>
                  <div className="step-content">
                    <h4>Authenticate</h4>
                    <p>Login with your credentials or register a new account</p>
                  </div>
                </div>
                <div className="step">
                  <span className="step-number">3</span>
                  <div className="step-content">
                    <h4>Access Protected Resources</h4>
                    <p>Once authenticated, you can access tenant-specific APIs</p>
                  </div>
                </div>
              </div>
            </div>
          </div>
        )}
      </main>

      <footer className="App-footer">
        <p>Built with React + ASP.NET Core BFF + Duende IdentityServer (Multi-Tenant)</p>
        <div className="footer-links">
          <a href={API_BASE_URL} target="_blank" rel="noopener noreferrer">
            BFF Server
          </a>
          <span>•</span>
          <span>Current Tenant: {tenantInfo?.id}</span>
        </div>
      </footer>
    </div>
  );
}

function App() {
  return (
    <Router>
      <Routes>
        <Route path="/" element={<HomePage />} />
        <Route path="/admin" element={<AdminDashboard />} />
      </Routes>
    </Router>
  );
}

export default App;