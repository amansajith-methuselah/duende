import React, { useState, useEffect } from 'react';
import './App.css';

function App() {
  const [user, setUser] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  // API Base URL
  const API_BASE_URL = 'https://localhost:5001';

  // Fetch user info on component mount
  useEffect(() => {
    checkAuthentication();
  }, []);

  // Check if user is authenticated
  const checkAuthentication = async () => {
    try {
      setLoading(true);
      const response = await fetch(`${API_BASE_URL}/api/user`, {
        credentials: 'include',
        headers: {
          'X-CSRF': '1'
        }
      });

      if (response.ok) {
        const data = await response.json();
        if (data.isAuthenticated) {
          setUser(data);
        } else {
          setUser(null);
        }
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
  };

  // Handle login - Use custom auth login endpoint
  const handleLogin = () => {
    // Redirect to custom login endpoint that will redirect back to React app
    window.location.href = `${API_BASE_URL}/auth/login`;
  };

  // Handle logout - Use custom auth logout endpoint
  const handleLogout = () => {
    // Redirect to custom logout endpoint that will redirect back to React app
    window.location.href = `${API_BASE_URL}/auth/logout`;
  };

  // Get user profile details
  const getUserProfile = async () => {
    try {
      const response = await fetch(`${API_BASE_URL}/api/user/profile`, {
        credentials: 'include',
        headers: {
          'X-CSRF': '1'
        }
      });

      if (response.ok) {
        const profileData = await response.json();
        console.log('User Profile:', profileData);
        alert('Check console for full profile details');
      } else {
        alert('Failed to get profile. Please login again.');
      }
    } catch (err) {
      console.error('Error getting profile:', err);
      alert('Error getting profile');
    }
  };

  // Test API endpoint
  const testApi = async () => {
    try {
      const response = await fetch(`${API_BASE_URL}/api/api/test`, {
        credentials: 'include',
        headers: {
          'X-CSRF': '1'
        }
      });

      if (response.ok) {
        const data = await response.json();
        console.log('API Test Response:', data);
        alert(`API Test Success! Message: ${data.message}`);
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

  return (
    <div className="App">
      <header className="App-header">
        <div className="header-content">
          <h1>🔐 React BFF Demo</h1>
          {user && user.isAuthenticated ? (
            <div className="user-info">
              <span className="welcome-text">
                Welcome, <strong>{user.username || user.claims?.name || 'User'}</strong>!
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

        {user && user.isAuthenticated ? (
          <div className="authenticated-content">
            <div className="welcome-card">
              <h2>🎉 You are logged in!</h2>
              <p>Welcome to the React application with BFF authentication pattern.</p>
            </div>

            <div className="user-details-card">
              <h3>📋 User Information</h3>
              <div className="user-details">
                <div className="detail-item">
                  <span className="label">Username:</span>
                  <span className="value">{user.username || 'N/A'}</span>
                </div>
                <div className="detail-item">
                  <span className="label">Email:</span>
                  <span className="value">{user.email || 'N/A'}</span>
                </div>
                {user.claims && (
                  <>
                    <div className="detail-item">
                      <span className="label">Name:</span>
                      <span className="value">{user.claims.name || 'N/A'}</span>
                    </div>
                    <div className="detail-item">
                      <span className="label">Phone:</span>
                      <span className="value">{user.claims.phone_number || 'Not provided'}</span>
                    </div>
                    <div className="detail-item">
                      <span className="label">Country:</span>
                      <span className="value">{user.claims.country || 'Not provided'}</span>
                    </div>
                  </>
                )}
              </div>
            </div>

            <div className="actions-card">
              <h3>🚀 Available Actions</h3>
              <div className="action-buttons">
                <button onClick={getUserProfile} className="btn btn-action">
                  Get Full Profile
                </button>
                <button onClick={testApi} className="btn btn-action">
                  Test API Endpoint
                </button>
                <button 
                  onClick={() => window.open(`${API_BASE_URL}/Account/Profile`, '_blank')} 
                  className="btn btn-action">
                  View Profile Page
                </button>
              </div>
            </div>

            {user.claims && (
              <div className="claims-card">
                <h3>🔑 User Claims</h3>
                <div className="claims-list">
                  {Object.entries(user.claims).map(([key, value]) => (
                    <div key={key} className="claim-item">
                      <span className="claim-key">{key}:</span>
                      <span className="claim-value">{value}</span>
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
              <h2>Welcome to React BFF Demo</h2>
              <p>This application demonstrates the Backend-For-Frontend (BFF) pattern with:</p>
              <ul className="features-list">
                <li>✅ Duende IdentityServer for authentication</li>
                <li>✅ ASP.NET Core BFF middleware</li>
                <li>✅ React frontend with secure API calls</li>
                <li>✅ Cookie-based authentication</li>
                <li>✅ CSRF protection</li>
              </ul>
              <button onClick={handleLogin} className="btn btn-primary btn-large">
                🔐 Login to Get Started
              </button>
            </div>

            <div className="info-card">
              <h3>📚 How It Works</h3>
              <div className="info-steps">
                <div className="step">
                  <span className="step-number">1</span>
                  <div className="step-content">
                    <h4>Click Login</h4>
                    <p>You'll be redirected to the Identity Server</p>
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
                    <p>Once authenticated, you can access all protected APIs</p>
                  </div>
                </div>
              </div>
            </div>
          </div>
        )}
      </main>

      <footer className="App-footer">
        <p>Built with React + ASP.NET Core + Duende IdentityServer</p>
        <div className="footer-links">
          <a href={`${API_BASE_URL}`} target="_blank" rel="noopener noreferrer">
            BFF Server
          </a>
          <span>•</span>
          <a href="https://localhost:7140" target="_blank" rel="noopener noreferrer">
            Identity Server
          </a>
        </div>
      </footer>
    </div>
  );
}

export default App;