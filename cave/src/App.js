import './App.css';
import Nav from "./Components/Nav"
import CommentsBar from "./Components/CommentsBar"
import Holder from "./Components/Login/Holder"
import { Route } from "react-router-dom";
import Login from "./Components/Login/Login"
import Register from './Components/Login/Register';

function App() {
  return (
    <div className="App">
      <Route exact path="/" component={Holder}/>
      <Nav/>
      <Route exact path="/" component={CommentsBar}/>
      <Route exact path="/login" component={Login}/>
      <Route exact path="/register" component={Register}/>
    </div>
  );
}

export default App;
