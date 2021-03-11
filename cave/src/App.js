import './App.css';
import Nav from "./Components/Nav"
import Holder from "./Components/Login/Holder"
import { Route } from "react-router-dom";
import Login from "./Components/Login/Login"
import Register from './Components/Login/Register';
import Home from "./Components/Home"
import Game from "./Components/Game"
import { useState } from 'react';
import { UserContext } from "./UserContext"

function App() {
  const [loggedIn, setLoggedIn] = useState(false)

  console.log(loggedIn)

  return (
    <div className="App">
      <UserContext.Provider value={loggedIn}>

        <Route path="/" exact 
              render={() => {
                  return(
                    <Holder loggedIn={loggedIn} setLoggedIn={setLoggedIn}/>
                  )
                }
              }
        />
        <Nav/>
        <Route exact path="/" component={Game}/>
        <Route exact path="/" component={Home}/>
        <Route path="/login" 
              render={() => {
                  return(
                    <Login setLoggedIn={setLoggedIn} loggedIn={loggedIn} />
                  )
                }
              }
        />
        <Route exact path="/register" component={Register}/>
      </UserContext.Provider>

    </div>
  );
}

export default App;
