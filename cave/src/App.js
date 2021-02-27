import './App.css';
import Nav from "./Components/Nav"
import Holder from "./Components/Login/Holder"
import { Route } from "react-router-dom";
import Login from "./Components/Login/Login"
import Register from './Components/Login/Register';
import Home from "./Components/Home"
import Game from "./Components/Game"
// import Index from "./Dagame/index.html"

function App() {
  return (
    <div className="App">
      <Route exact path="/" component={Holder}/>
      <Nav/>
      {/* <Index/> */}
      <Route exact path="/" component={Game}/>
      <Route exact path="/" component={Home}/>
      <Route exact path="/login" component={Login}/>
      <Route exact path="/register" component={Register}/>
    </div>
  );
}

export default App;
