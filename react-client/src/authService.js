import axios from 'axios';

const API_BASE_URL = 'https://localhost:5001';

// Configure axios to include credentials (cookies)
axios.defaults.withCredentials = true;
axios.defaults.baseURL = API_BASE_URL;

export const authService = {
  // Check if user is authenticated
  async getUser() {
    try {
      console.log('Calling /api/user...');
      const response = await axios.get('/api/user');
      console.log('Response from /api/user:', response.data);
      return response.data;
    } catch (error) {
      console.error('Error getting user:', error);
      console.error('Error details:', error.response);
      return { isAuthenticated: false };
    }
  },

  // Initiate login
  login() {
    window.location.href = `${API_BASE_URL}/bff/login`;
  },

  // Initiate logout
  logout() {
    // Full page redirect to trigger OIDC logout flow
    window.location.href = `${API_BASE_URL}/logout`;
  },

  // Get data from API
  async getData() {
    try {
      const response = await axios.get(`${API_BASE_URL}/api/data`);
      return response.data;
    } catch (error) {
      console.error('Error getting data:', error);
      throw error;
    }
  }
};